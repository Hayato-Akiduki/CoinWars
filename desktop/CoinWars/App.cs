using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace CoinWars;

public class App : Application
{
    [STAThread]
    public static void Main()
    {
        using var mutex = new Mutex(true, "CoinWars.Desktop.SingleInstance", out bool owner);
        if (!owner) { MessageBox.Show("CoinWarsは既に起動しています。"); return; }
        new App().Run(new GameWindow());
    }
}

public record Preferences(bool Muted = false, bool ReducedMotion = false, int TextPercent = 100);
public partial class GameWindow : Window
{
    readonly Game game = new();
    readonly DispatcherTimer timer = new();
    readonly Canvas scene = new() { Width = 1440, Height = 900, ClipToBounds = true };
    readonly Canvas effects = new() { Width = 1440, Height = 900, IsHitTestVisible = false, ClipToBounds = true };
    readonly Grid overlays = new() { Width = 1440, Height = 900 };
    readonly Grid surface = new();
    readonly ScrollViewer viewport = new() { HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled, HorizontalContentAlignment=HorizontalAlignment.Center };
    readonly Grid modalHost = new();
    readonly Grid undoHost = new() {VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(12)};
    readonly Dictionary<(bool player, int slot), FrameworkElement> actors = [];
    readonly string savePath;
    readonly bool preview;
    Preferences preferences = new();
    (bool bench, int index)? selected;
    int? selectedItem;
    int speed = 1;
    bool paused, enemyPreview, shopOpen = true, resultPending, animating;
    string message = "肖像で詳細、購入ボタンで雇用。キャラ・装備はドラッグで配置。右クリックで選択解除。";
    string? dialog;
    Point dragStart;
    bool dragging;
    int transitionGeneration;

    public GameWindow()
    {
        preview = Environment.GetCommandLineArgs().Contains("--preview");
        savePath = preview ? System.IO.Path.Combine(AppContext.BaseDirectory, "preview", "save.json") : Saves.FilePath;
        Title = "CoinWars";
        Width = Math.Min(1440, SystemParameters.WorkArea.Width); Height = Math.Min(940, SystemParameters.WorkArea.Height);
        MinWidth = Math.Min(640,SystemParameters.WorkArea.Width); MinHeight = Math.Min(420,SystemParameters.WorkArea.Height);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = B("#080C1A"); FontFamily = new FontFamily("Yu Gothic UI");
        surface.ClipToBounds=true;
        surface.Children.Add(scene); surface.Children.Add(effects);
        viewport.Content = new Viewbox { Stretch = Stretch.Uniform, StretchDirection=StretchDirection.DownOnly, Child = surface };
        var root=new Grid();root.Children.Add(viewport);
        root.Children.Add(undoHost);
        modalHost.Children.Add(new Viewbox {Stretch=Stretch.Uniform,Child=overlays});root.Children.Add(modalHost);
        Content=root;
        if(preview&&Environment.GetCommandLineArgs().Contains("--small")){Width=Math.Min(960,SystemParameters.WorkArea.Width);Height=Math.Min(640,SystemParameters.WorkArea.Height);}
        try
        {
            var loaded = Saves.ReadWithRecovery(savePath);
            if (loaded.State != null) game.S = loaded.State; else game.New();
            if (loaded.Recovered) message = "バックアップからセーブを復元しました。";
        }
        catch (Exception ex)
        {
            game.New(); message = "保存データを復元できませんでした：" + ex.Message;
            try { Saves.ArchiveInvalid(savePath); } catch { }
        }
        if (preview && !File.Exists(savePath))
        {
            game.S.Level = 6; game.S.Gold = 48; game.S.Round = 7;
            game.S.Board = [new(){Key="swordman",Star=2},new(){Key="shield"},null,new(){Key="captain"},new(){Key="healer"},null,new(){Key="witch"},new(){Key="archer"}];
            game.S.Bench[0] = new(){Key="banker"}; game.S.Bench[1] = new(){Key="swordman"};
            game.S.Items = ["blade","tome","amulet"];
            game.Roll(); game.MakeEnemy();
        }
        try { if (File.Exists(SettingsPath)) preferences = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(SettingsPath)) ?? new(); } catch { }
        textScale = (preferences.TextPercent is 115 or 130 ? preferences.TextPercent : 100) / 100.0;
        SetSpeed();
        timer.Tick += (_, _) => Tick();
        Closing += (_, _) => { transitionGeneration++; timer.Stop(); ClearPresentation(); Persist(); };
        PreviewKeyDown += OnKey;
        MouseRightButtonUp += (_,e) => { if(dialog!=null)SetDialog(null);else if(Planning){selected=null;selectedItem=null;Render();}e.Handled=true; };
        SizeChanged += (_,_) => { if(!ShowingBattle&&!animating&&CompactLayout!=WantsCompactLayout)Render(); };
        Render(); Persist();
    }
    string SettingsPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(savePath)!, "preferences.json");
    void Persist()
    {
        try { Saves.Write(game.S, savePath); }
        catch (Exception ex) { message = "保存できません：" + ex.Message; }
    }
    void SavePreferences()
    {
        try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath)!); File.WriteAllText(SettingsPath, JsonSerializer.Serialize(preferences)); }
        catch (Exception ex) { message = "設定を保存できません：" + ex.Message; }
    }
    void Sound(string name) { if (!preferences.Muted) GameAudio.Play(name); }
    void SetSpeed() { timer.Interval = TimeSpan.FromMilliseconds(16); SyncPlayback(); }
    void Execute(Func<string> action, string success = "", string sound = "click")
    {
        message = action();
        if (message.Length == 0) { message = success; Sound(sound); }
        Persist(); Render();
    }
    void Buy(int index)
    {
        selected = null;
        Execute(() => game.Buy(index), "雇用したキャラクターは待機エリアに加わります。", "buy");
        if (game.LastMerge is {} merge)
        {
            Sound("merge");
            Toast("RANK UP", Data.Def(merge.Key).Name + "   " + new string('★', merge.Star), Gold);
        }
    }
    async void Tick()
    {
        if (Suspended || resultPending || animating || game.S.Phase != "battle") return;
        animating = true;
        int generation = transitionGeneration;
        try
        {
            game.Step();
            var cue = game.LastCue;
            resultPending = game.S.Phase != "battle";
            if (cue is { IsSkill: true } && !preferences.ReducedMotion)
            {
                SkillCutIn(cue);
                if (!await WaitPresentation(460, generation)) return;
            }
            if (!resultPending)
            {
                Render();
                PlayCue(cue);
                if (!await WaitPresentation(cue?.IsSkill == true && !preferences.ReducedMotion ? 420 : 650, generation)) return;
            }
            else
            {
                timer.Stop();
                UpdateBattleActors();
                PlayCue(cue);
                Persist();
                if (!await WaitPresentation(preferences.ReducedMotion ? 100 : 800, generation)) return;
                string result = game.S.Results.LastOrDefault() ?? "draw";
                Sound(result == "win" ? "win" : "lose");
                ShowRoundResult(result);
                if (!await WaitPresentation(preferences.ReducedMotion ? 600 : 1800, generation)) return;
                resultPending = false;
                animating = false;
                Render();
            }
        }
        finally
        {
            animating = false;
        }
    }
    async void Fight()
    {
        if (!Planning) return;
        if (game.Count == 0) { message = "キャラクターを1体以上配置してください。"; Render(); return; }
        Persist();
        if (!game.Start()) return;
        selected = null; selectedItem = null; enemyPreview = false; paused = false;
        animating = true;
        int generation = transitionGeneration;
        Sound("start"); Render(); viewport.ScrollToTop(); BattleEntrance();
        try { if (await WaitPresentation(preferences.ReducedMotion ? 200 : 760, generation)) timer.Start(); }
        finally { animating = false; }
    }
    Unit? SelectedUnit() => selected is {} x ? (x.bench ? game.S.Bench : game.S.Board)[x.index] : null;
    void Slot(bool bench, int index)
    {
        if (!Planning) return;
        var unit = (bench ? game.S.Bench : game.S.Board)[index];
        if (selectedItem is int it && unit != null)
        {
            selectedItem = null; Execute(() => game.Equip(unit, it), "装備しました。", "buy"); return;
        }
        if (selected is {} from)
        {
            selected = null;
            Execute(() => game.Move(from.bench, from.index, bench, index), "編成を更新しました。");
        }
        else if (unit != null) { selected = (bench, index); Sound("click"); Render(); }
    }
    void Sell()
    {
        if (selected is not {} x || !Planning) return;
        game.Sell(x.bench, x.index); selected = null; selectedItem=null;message=game.SoldName+"を売却しました。取り消しできます。";Sound("buy"); Persist(); Render();
    }
    void Restart()
    {
        transitionGeneration++; timer.Stop(); ClearPresentation(); resultPending = false; paused = false;
        selected = null; selectedItem = null; enemyPreview = false; dialog = null;
        game.New(); SyncPlayback(); Persist(); Render(); Sound("start");
    }
    void OnKey(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat) return;
        if (e.Key == Key.M) { ToggleMute(); e.Handled = true; return; }
        if (e.Key == Key.P && dialog == null) { TogglePause(); e.Handled = true; return; }
        if (e.Key == Key.F11) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; e.Handled = true; return; }
        if (e.Key == Key.Escape)
        {
            if (dialog != null) SetDialog(null);
            else if (!animating && !resultPending) { selected = null; selectedItem = null; enemyPreview = false; Render(); }
            e.Handled = true; return;
        }
        if (dialog != null) return;
        if (e.Key == Key.H) { SetDialog("help"); e.Handled = true; return; }
        if (!Planning) return;
        if (e.Key >= Key.D1 && e.Key <= Key.D5) Buy(e.Key - Key.D1);
        else switch (e.Key)
        {
            case Key.D: Execute(game.Reroll, "ショップを更新しました。", "shuffle"); break;
            case Key.F: Execute(game.BuyExp, "経験値を購入しました。", "buy"); break;
            case Key.E: Sell(); break;
            case Key.Space: Fight(); break;
            default: return;
        }
        e.Handled = true;
    }

    void Render()
    {
        scene.Children.Clear(); actors.Clear(); pauseButton = null; speedButton = null;
        ConfigureLayout();
        if(CompactLayout) RenderCompactLayout();
        else
        {
            BackgroundScene();Header();Synergies();Stage();Sidebar();Bench();Shop();
            Put(scene, Label(message, 13, Muted), 28, 866, 1120, 25);
            Put(scene, Label("P 停止   H ヘルプ   M 音声", 12, Muted), 1178, 868, 240, 24);
        }
        RenderSaleUndo();
        RenderOverlays();
    }
    void Header()
    {
        var s = game.S;
        Put(scene, Label("CoinWars", 25, White, true), 28, 20, 225, 36);
        Put(scene, Label("CoinWars", 12, Muted), 30, 53, 220, 24);
        Put(scene, Plate("#E90A1126", "#53637E"), 272, 18, 882, 76);
        Put(scene, Label(s.Phase == "battle" ? "戦闘フェーズ" : s.Phase == "event" ? "投資の機会" : "準備フェーズ", 13, Cyan), 290, 28, 160, 23);
        Put(scene, Label($"{s.Round:00} / 20", 25, White, true), 290, 49, 140, 36);
        for (int i = 0; i < 20; i++)
        {
            double x = 445 + i * 32;
            var color = i == s.Round - 1 ? Gold : i < s.Results.Count ? (s.Results[i] == "win" ? Cyan : B("#CE7D93")) : B("#5D6685");
            if (i < 19) Line(scene, x + 12, 55, x + 31, 55, "#58617A", 1);
            var diamond = new Rectangle { Fill = i < s.Round ? color : B("#192038"), Stroke = color, StrokeThickness = 1.5, RenderTransform = new RotateTransform(45), RenderTransformOrigin = new Point(.5, .5) };
            Put(scene, diamond, x, 48, 14, 14);
            if ((i + 1) % 5 == 0) Put(scene, Label("◆", 10, Gold), x + 1, 69, 22, 20);
        }
        Put(scene, Label("♥", 23, B("#FF9CB2")), 1178, 28, 34, 32);
        Put(scene, Label(s.Hp.ToString(), 30, White, true), 1216, 22, 70, 42);
        Put(scene, Meter(s.Hp / 100.0, "#F09AAC"), 1180, 68, 102, 5);
        Put(scene, Btn("☷", () => SetDialog("settings")), 1300, 23, 48, 45);
        Put(scene, Btn("?", () => SetDialog("help")), 1360, 23, 48, 45);
    }
    void Synergies()
    {
        Put(scene, Label("絆", 24, White, true), 30, 119, 180, 36);
        Put(scene, Label("編成ボーナス", 11, Muted), 30, 153, 200, 20);
        var counts = game.Counts(game.S.Board);
        string[] descriptions = ["攻撃力を高める", "最大HP・開幕シールド", "スキル威力と回転率", "速度・開幕即行動", "追加収入・所持金で強化"];
        int i = 0;
        foreach (var (key, name) in Data.Traits)
        {
            int tier = game.Tier(key); double y = 189 + i * 69;
            Put(scene, Plate(tier > 0 ? "#252E3E" : "#161D2B", "#293344"), 24, y, 208, 60);
            Put(scene, Label(TraitSymbol(key), 24, tier > 0 ? Gold : Muted), 38, y + 10, 35, 35);
            Put(scene, Label(name, 16, White, true), 79, y + 7, 110, 25);
            Put(scene, Label(counts[key].ToString(), 21, tier > 0 ? Gold : White, true), 197, y + 7, 30, 30);
            Put(scene, Label(string.Join("  ›  ", Data.Thresholds[key]), 11, tier > 0 ? Gold : Muted), 80, y + 32, 130, 20);
            var tip = descriptions[i] + "\n" + string.Join(" / ", Data.Units.Where(d => d.Traits.Contains(key)).Select(d => d.Name));
            ((FrameworkElement)scene.Children[^4]).ToolTip = tip;
            i++;
        }
        Put(scene, Label("収益予測", 14, Gold), 30, 530, 180, 25);
        Put(scene, Label($"利息 +{game.Interest}   /   上限 {game.S.InterestCap}", 16, White), 30, 558, 200, 25);
        Put(scene, Label($"勝利時 +{game.Income(true,game.S.Streak > 0 ? game.S.Streak + 1 : 1)} コイン", 13, Muted), 30, 584, 205, 22);
    }
    void Stage()
    {
        bool battle = ShowingBattle;
        Put(scene, battleStatus = Label(battle ? (Suspended ? "PAUSED" : "AUTO BATTLE") : enemyPreview ? "競争相手の編成" : $"配置人数   {game.Count} / {game.Capacity}", 24, Cyan, true), 540, 108, 420, 40);
        if (battle)
        {
            Put(scene, pauseButton = Btn(paused ? "▶ 再開" : "Ⅱ 停止", TogglePause), 950, 108, 100, 36);
            Put(scene, speedButton = Btn($"×{speed}", CycleSpeed), 1064, 108, 70, 36);
            TurnOrder();
            foreach (var f in game.Fighters)
            {
                var anchor = BattleAnchor(f.Player, f.Slot);
                var actor = Actor(f.Unit, f, false, f.Slot, true);
                actors[(f.Player, f.Slot)] = actor;
                Put(scene, actor, anchor.X - 48, anchor.Y - 143, 96, 174);
            }
            Put(scene, Label("味方", 13, Cyan), 355, 535, 200, 22);
            Put(scene, Label("競争相手", 13, B("#F6A1BA")), 925, 535, 200, 22);
            Put(scene, Label($"行動 {game.Actions} / 241", 11, Muted), 653, 545, 190, 22);
        }
        else
        {
            var units = enemyPreview ? game.S.Enemy : game.S.Board;
            Put(scene, Label(enemyPreview ? "相手の前衛" : "前衛エリア", 12, Muted), 654, 160, 150, 24);
            Put(scene, Label(enemyPreview ? "相手の後衛" : "後衛エリア", 12, Muted), 654, 376, 150, 24);
            for (int index = 0; index < 8; index++)
            {
                double x = 350 + index % 4 * 187;
                double y = index < 4 ? 188 : 403;
                var slot = Actor(units[index], null, false, index, enemyPreview);
                Put(scene, slot, x, y, 142, index < 4 ? 178 : 136);
                actors[(!enemyPreview, index)] = slot;
            }
            if (enemyPreview)
                Put(scene, Btn("自分の編成へ戻る", () => { enemyPreview = false; Render(); }, true, true), 598, 552, 265, 44);
            else
                Put(scene, Btn("戦闘開始 ▶", Fight, Planning && game.Count > 0, true), 903, 548, 244, 49);
            Put(scene, Label(selected!=null?"配置先をクリック / ドラッグで移動 / 右クリックで解除":"前衛・後衛へ配置して、戦闘開始。", 13, Muted), 282, 555, 600, 38);
        }
    }
    void Sidebar()
    {
        Put(scene, Plate("#D711182B", "#3B4866"), 1190, 117, 226, 480);
        var unit = SelectedUnit();
        if (unit != null)
        {
            var d = Data.Def(unit);
            Put(scene, Label("CHARACTER", 10, Gold), 1207, 131, 194, 22);
            Put(scene, Label(d.Name, 21, White, true), 1207, 158, 195, 36);
            Put(scene, Label(new string('★',unit.Star) + "    " + string.Join(" / ",d.Traits.Select(t => Data.Traits[t])), 12, Gold), 1207, 196, 194, 28);
            Put(scene, Label($"HP  {d.Hp * Data.Mult(unit.Star):0}\nATK  {d.Atk * Data.Mult(unit.Star):0}\n速度  {d.Spd}", 16, White), 1207, 232, 194, 85);
            Put(scene, Label(Data.Skills[d.Skill], 12, Muted), 1207, 322, 194, 40);
            for (int j = 0; j < unit.Items.Count; j++)
            {
                int index = j; var it = Data.Items.First(x => x.Key == unit.Items[index]);
                int wait = Math.Max(0, unit.Equipped[index] + 2 - game.S.Round);
                Put(scene, Btn(it.Name + (wait > 0 ? $"  {wait}R" : "  外す"),
                    () => Execute(() => game.Unequip(unit,index), "装備を外しました。"),
                    Planning), 1204, 370 + j * 40, 198, 35);
            }
            Put(scene, Btn("前衛へ",()=>MoveSelection(0),Planning),1204,494,96,42);
            Put(scene, Btn("後衛へ",()=>MoveSelection(1),Planning),1306,494,96,42);
            Put(scene, Btn(selected?.bench==true?"選択解除":"待機へ",()=>{if(selected?.bench==true){selected=null;Render();}else MoveSelection(2);},Planning),1204,544,96,42);
            Put(scene, Btn($"売却 {game.SellValue(unit)} ◈", Sell, Planning),1306,544,96,42);
            return;
        }
        Put(scene, Label("競争相手", 16, White, true), 1206, 132, 200, 28);
        Put(scene, Label(game.S.Round == 20 ? "最終決戦" : game.S.Round % 5 == 0 ? "精鋭部隊" : "次の対戦", 12, Gold), 1206, 164, 196, 22);
        var enemy = game.S.Enemy.OfType<Unit>().ToArray();
        for (int i = 0; i < enemy.Length; i++)
        {
            var u = enemy[i];
            var image = Portrait(u.Key);
            Put(scene, image, 1206 + i % 4 * 49, 194 + i / 4 * 55, 45, 51);
            image.ToolTip = Data.Def(u).Name + " " + new string('★', u.Star);
        }
        Put(scene, Btn(enemyPreview ? "自分の編成" : "編成を確認", () => { enemyPreview = !enemyPreview; Render(); }, Planning), 1204, 306, 198, 36);
        Put(scene, Label("装備バッグ", 16, White, true), 1206, 356, 198, 26);
        for (int i = 0; i < game.S.Items.Count; i++)
        {
            int index = i; var it = Data.Items.First(d => d.Key == game.S.Items[index]);
            var b = Btn(ItemSymbol(it.Key), () => { selectedItem = index; selected = null; message = it.Name + "：" + it.Description + "。装備する味方を選択。"; Render(); }, Planning, selectedItem == index);
            b.ToolTip = it.Name + "\n" + it.Description;
            EnableEquipmentDrag(b,index);
            Put(scene, b, 1205 + i % 4 * 49, 394 + i / 4 * 45, 44, 40);
        }
        if (game.S.Items.Count == 0) Put(scene, Label("戦闘やイベントで獲得", 12, Muted), 1206, 399, 198, 28);
        Put(scene, Btn("戦闘レポート", () => SetDialog("report")), 1204, 544, 198, 42);
    }
    void Bench()
    {
        Put(scene, Plate("#F0101627", "#45506C"), 24, 606, 1392, 94);
        Put(scene, Label($"Lv.{game.S.Level}", 30, White, true), 42, 616, 140, 43);
        Put(scene, Label($"EXP {game.S.Exp} / {(game.S.Level == 9 ? "MAX" : Data.Exp[game.S.Level])}", 11, Muted), 43, 659, 145, 22);
        Put(scene, Meter(game.S.Level == 9 ? 1 : (double)game.S.Exp/Data.Exp[game.S.Level], "#B8DBEF"), 43, 684, 140, 4);
        Put(scene, Btn("EXP +4\n4 ◈ [F]", () => Execute(game.BuyExp, "経験値を購入しました。", "buy"), Planning && game.S.Level < 9 && game.S.Gold >= 4), 198, 619, 105, 67);
        for (int i = 0; i < 8; i++) Put(scene, Actor(game.S.Bench[i], null, true, i, false), 324 + i * 97, 614, 90, 78);
        Put(scene, Label("◈", 30, Gold), 1121, 626, 40, 40);
        Put(scene, Label(game.S.Gold.ToString(), 33, Gold, true), 1164, 623, 95, 45);
        Put(scene, Btn(shopOpen ? "ショップ ▾" : "ショップ ▴", () => { if (!animating && !resultPending) { shopOpen = !shopOpen; Render(); } }), 1263, 627, 137, 47);
    }
    void Shop() => MouseShop();

    void EventDialog()
    {
        var panel = Modal("投資の機会", "戦いの先を見据えて、ひとつの選択を。", 1000, 470);
        var cards = new UniformGrid { Columns = 3, Margin = new Thickness(24,34,24,18) };
        for (int i = 0; i < game.S.Offers.Count; i++)
        {
            int index = i; var offer = game.S.Offers[i];
            var content = new StackPanel { Margin = new Thickness(22) };
            content.Children.Add(Label(new[]{"◇","✦","◈"}[i], 52, Gold));
            content.Children.Add(Label(offer.Title, 24, White, true));
            content.Children.Add(Label(offer.Description, 16, Muted));
            content.Children.Add(Label("選択する  →", 13, Gold));
            var b = Btn("", () => { game.Choose(index); Sound("merge"); Persist(); Render(); Toast("INVESTMENT",offer.Title,Gold); });
            b.Content = content; b.Height = 270; b.Margin = new Thickness(8); cards.Children.Add(b);
        }
        panel.Children.Add(cards);
    }
    void EndDialog()
    {
        bool win = game.S.Hp > 0 && game.S.Results.LastOrDefault() == "win";
        var panel = Modal(win ? "TRIUMPH" : "GAME OVER", win ? "全20ラウンド制覇" : "次の投資が、新たな勝機になる。", 720, 420);
        panel.Children.Add(Label($"到達 {game.S.Round} ラウンド     HP {game.S.Hp}     コイン {game.S.Gold}", 22, White));
        panel.Children.Add(Label($"自己ベスト R{game.S.BestRound} / クリア {game.S.Clears}回", 15, Muted));
        var next = Btn("もう一度挑戦する", Restart, true, true); next.Margin = new Thickness(80,40,80,0); next.Height = 55; panel.Children.Add(next);
    }
    StackPanel Modal(string title, string subtitle, double width, double height)
    {
        overlays.Children.Add(new Border { Background = B("#CA040815") });
        var panel = new StackPanel { Margin = new Thickness(30) };
        panel.Children.Add(Label(title, 34, Gold, true));
        panel.Children.Add(Label(subtitle, 15, Muted));
        overlays.Children.Add(new Border { Width = Math.Min(width,overlays.Width-32), Height = Math.Min(height,overlays.Height-32), Background = new LinearGradientBrush(ColorOf("#202A43"), ColorOf("#0E1528"),90),
            BorderBrush = Gold, BorderThickness = new Thickness(1), Child = new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }
    void UtilityDialog()
    {
        if (dialog == "compare") { ComparisonDialog(); return; }
        var panel = Modal(dialog == "help" ? "遊び方" : dialog == "report" ? "戦闘レポート" : dialog == "restart" ? "新しいゲーム" : "設定", "", 820, 590);
        if (dialog == "help")
        {
            panel.Children.Add(Label("① ショップでキャラクターを雇用\n② 待機エリアからドラッグ、または選択→配置先で編成\n③ 同じ種類・同じ★を3体集めてランクアップ\n④ 異なるキャラクターの絆を揃えてボーナスを発動\n⑤ 装備を選び、味方をクリックして装備\n⑥ 戦闘開始で自動バトル。20ラウンド目のボスを倒そう。",20,White));
            panel.Children.Add(Label("1～5 購入 / D 更新 / F 経験値 / E 売却 / Space 戦闘開始\nP 一時停止 / M 音声切替 / Esc 選択解除 / F11 最大化\n装備は2ラウンド後に解除可能。10コインごとに利子+1。\n準備中は自動保存。戦闘中に終了すると戦闘直前から再開。",15,Muted));
        }
        else if (dialog == "report")
        {
            var report = new StackPanel();
            double max = Math.Max(1,game.Fighters.Where(f => f.Player).Select(f => f.Dealt).DefaultIfEmpty(1).Max());
            foreach (var f in game.Fighters.Where(f => f.Player).OrderByDescending(f => f.Dealt))
            {
                report.Children.Add(Label(Data.Def(f.Unit).Name + $"     {f.Dealt:0} ダメージ",16,White));
                var meter = Meter(f.Dealt/max, "#DFBF82"); meter.Height=7; meter.Margin=new Thickness(0,0,0,8); report.Children.Add(meter);
            }
            report.Children.Add(Label(string.Join("\n",game.S.Log.Take(18)),13,Muted));
            panel.Children.Add(new ScrollViewer { Content=report,Height=350,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
        }
        else if (dialog == "restart")
        {
            panel.Children.Add(Label("進行中のゲームを終了して、最初から始めますか？\n自己ベストとクリア回数は引き継ぎます。",22,White));
            panel.Children.Add(Btn("新しいゲームを開始",Restart,true,true));
        }
        else
        {
            panel.Children.Add(Btn($"文字サイズ：{preferences.TextPercent}%（クリックで変更）", CycleTextSize));
            panel.Children.Add(Label("100% → 115% → 130%。説明画面はスクロールできます。",15,Muted));
            panel.Children.Add(Btn(preferences.Muted ? "効果音：OFF" : "効果音：ON", ToggleMute));
            panel.Children.Add(Btn(preferences.ReducedMotion ? "演出：控えめ" : "演出：通常", () => { preferences=preferences with {ReducedMotion=!preferences.ReducedMotion};SavePreferences();RenderOverlays(); }));
            panel.Children.Add(Btn("最初から", () => SetDialog("restart"),game.S.Phase!="battle"&&!resultPending));
            panel.Children.Add(Label($"記録：{game.S.Plays}回挑戦 / {game.S.Clears}回クリア\n準備中・イベント選択中は自動保存します。",16,Muted));
        }
        var close=Btn("閉じる  [Esc]",()=>SetDialog(null));close.Margin=new Thickness(0,24,0,0);close.Height=42;panel.Children.Add(close);
    }
}


