using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CoinWars;

public partial class GameWindow
{
    readonly Grid launchHost = new();
    bool enteredGame;
    string? launchNotice;

    void ShowSplash()
    {
        launchHost.Background = new RadialGradientBrush(ColorOf("#25374B"), ColorOf("#080C1A"));
        var splash = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var mark = new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/CoinWars;component/Assets/CoinWars.ico")),
            Width = 64, Height = 64
        };
        splash.Children.Add(mark);
        splash.Children.Add(Label("CoinWars", 42, White, true));
        splash.Children.Add(new TextBlock
        {
            Text = "読み込み中…", Foreground = Muted, FontSize = 15 * textScale,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 0)
        });
        launchHost.Children.Add(splash);
    }

    async void OnLaunchRendered(object? sender, EventArgs args)
    {
        ContentRendered -= OnLaunchRendered;
        var splashTime = preferences.ReducedMotion ? Task.CompletedTask : Task.Delay(450);
        await Dispatcher.Yield(DispatcherPriority.Background);
        if (!IsVisible) return;
        bool hasSave = LoadLaunchSave();
        await splashTime;
        if (IsVisible) ShowTitle(hasSave);
    }

    void ShowTitle(bool hasSave)
    {
        launchHost.Children.Clear();
        launchHost.Background = new LinearGradientBrush(ColorOf("#101725"), ColorOf("#344558"), 24);

        var artwork = Portrait("dragon", PortraitFraming.UpperBody);
        artwork.HorizontalAlignment = HorizontalAlignment.Right;
        artwork.VerticalAlignment = VerticalAlignment.Bottom;
        artwork.SetBinding(WidthProperty, new System.Windows.Data.Binding(nameof(ActualHeight)) { Source = launchHost });
        artwork.SetBinding(HeightProperty, new System.Windows.Data.Binding(nameof(ActualHeight)) { Source = launchHost });
        artwork.IsHitTestVisible = false;
        launchHost.Children.Add(artwork);

        var shade = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
        shade.GradientStops.Add(new GradientStop(ColorOf("#FF101725"), 0));
        shade.GradientStops.Add(new GradientStop(ColorOf("#F4101725"), .40));
        shade.GradientStops.Add(new GradientStop(ColorOf("#32101725"), 1));
        launchHost.Children.Add(new Border { Background = shade, IsHitTestVisible = false });

        var menu = new StackPanel { Width = 370, Margin = new Thickness(44, 24, 24, 24), HorizontalAlignment = HorizontalAlignment.Left };
        menu.Children.Add(Label("CoinWars", 54, White, true));
        var subtitle = Label("仲間を雇い、編成と投資で20ラウンドの制覇を目指す。", 16, Muted);
        subtitle.Margin = new Thickness(0, 6, 0, 22);
        menu.Children.Add(subtitle);

        var save = game.S;
        bool hasResult = hasSave && save.Phase == "over";
        if (hasSave)
        {
            string challenge = save.ChallengeNumber is int seed ? $"  /  挑戦番号 {seed}" : "";
            menu.Children.Add(Label($"ラウンド {save.Round} / 20  ·  HP {save.Hp}{challenge}", 15, Gold));
        }
        if (launchNotice is string notice)
            menu.Children.Add(Label(notice, 15, Gold));

        var start = LaunchButton("はじめから", () =>
        {
            bool hasExistingFile = hasSave || File.Exists(savePath) || File.Exists(savePath + ".bak");
            if (hasExistingFile &&
                MessageBox.Show(this, "現在の進行を置き換えて、新しいゲームを始めますか？",
                    "はじめから", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            EnterGame(true);
        }, !hasSave);
        var resume = LaunchButton(hasResult ? "前回の結果を見る" : "つづきから", () => EnterGame(false), true);
        if (hasSave) menu.Children.Add(resume);
        menu.Children.Add(start);
        menu.Children.Add(LaunchButton("セーブ一覧", () => ShowSaveHistory(hasSave)));
        menu.Children.Add(LaunchButton("リザルト一覧", () => ShowRunResults(hasSave)));
        menu.Children.Add(LaunchButton("スコアランキング", () => ShowScores(hasSave)));
        menu.Children.Add(LaunchButton("終了", Close));

        launchHost.Children.Add(new ScrollViewer
        {
            Content = menu, VerticalAlignment = VerticalAlignment.Center,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });
        Keyboard.Focus(hasSave ? resume : start);
    }

    void ReturnToTitle()
    {
        if (ShowingBattle) return;
        if (!Persist())
        {
            MessageBox.Show(this, message, "保存エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        enteredGame = false;
        dialog = null;
        selected = null;
        selectedItem = null;
        enemyPreview = false;
        highlightedPurchase = null;
        paused = false;
        timer.Stop();
        transitionGeneration++;
        ClearPresentation();
        SyncPlayback();
        launchHost.Visibility = Visibility.Visible;
        launchNotice = null;
        ShowTitle(true);
    }

    void ShowSaveHistory(bool hasSave)
    {
        var panel = OpenHistoryPage("セーブ一覧", "タイトルに戻る", () => ShowTitle(hasSave));
        if (!hasSave || game.S.Phase == "over")
        {
            panel.Children.Add(Label("進行中の挑戦はありません。", 18, Muted));
            return;
        }

        var state = game.S;
        panel.Children.Add(Label($"進行中   R{state.Round} / 20   HP {state.Hp}   {state.Gold} コイン", 18, Gold));
        panel.Children.Add(Label(RunMode(state.ChallengeNumber), 15, Muted));
        panel.Children.Add(LaunchButton("つづきから", () => EnterGame(false), true));
        AddBattleHistory(panel, state.BattleSaves);
    }

    void ShowRunResults(bool hasSave)
    {
        var panel = OpenHistoryPage("リザルト一覧", "タイトルに戻る", () => ShowTitle(hasSave));
        if (game.S.RunResults.Count == 0)
            panel.Children.Add(Label("終了した挑戦はまだありません。", 18, Muted));
        foreach (var run in game.S.RunResults.AsEnumerable().Reverse())
        {
            string summary = $"挑戦 {run.Attempt}   {RunOutcome(run.Outcome)}   R{run.Round} / 20   HP {run.Hp}";
            panel.Children.Add(LaunchButton(summary, () => ShowRunDetails(hasSave, run)));
        }
    }

    void ShowRunDetails(bool hasSave, RunResult run)
    {
        var panel = OpenHistoryPage($"挑戦 {run.Attempt}   {RunOutcome(run.Outcome)}", "リザルト一覧に戻る", () => ShowRunResults(hasSave));
        panel.Children.Add(Label($"R{run.Round} / 20   HP {run.Hp}   {run.Gold} コイン   {RunMode(run.ChallengeNumber)}", 18, Gold));
        AddBattleHistory(panel, run.Battles);
    }

    StackPanel OpenHistoryPage(string title, string backLabel, Action goBack)
    {
        launchHost.Children.Clear();
        launchHost.Background = new LinearGradientBrush(ColorOf("#101725"), ColorOf("#344558"), 24);
        var panel = new StackPanel { MaxWidth = 680, Margin = new Thickness(32, 24, 32, 24) };
        var back = LaunchButton(backLabel, goBack);
        back.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(back);
        panel.Children.Add(Label(title, 34, White, true));
        launchHost.Children.Add(new ScrollViewer
        {
            Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        });
        Keyboard.Focus(back);
        return panel;
    }

    static string RunMode(int? number) => number is int seed ? $"挑戦番号 {seed}" : "通常プレイ";
    static string RunOutcome(string outcome) => outcome switch { "clear" => "クリア", "abandoned" => "途中終了", _ => "敗北" };

    static void AddBattleHistory(StackPanel panel, List<BattleSave> battles)
    {
        var heading = Label("戦闘ごとの記録", 21, White, true);
        heading.Margin = new Thickness(0, 18, 0, 6);
        panel.Children.Add(heading);
        if (battles.Count == 0)
            panel.Children.Add(Label("完了した戦闘はまだありません。", 16, Muted));
        foreach (var battle in battles.AsEnumerable().Reverse())
        {
            string result = battle.Outcome switch { "win" => "勝利", "lose" => "敗北", "draw" => "引き分け", _ => "結果不明" };
            string details = battle.Hp is int hp && battle.Gold is int gold ? $"   HP {hp}   {gold} コイン" : "   HP・コインの記録なし";
            var card = Plate("#E3222C42", "#596680");
            card.Margin = new Thickness(0, 0, 0, 6);
            card.Padding = new Thickness(12, 8, 12, 8);
            card.Child = Label($"R{battle.Round}   {result}{details}", 16, White);
            panel.Children.Add(card);
        }
    }

    void ShowScores(bool hasSave)
    {
        launchHost.Children.Clear();
        launchHost.Background = new LinearGradientBrush(ColorOf("#101725"), ColorOf("#344558"), 24);
        var panel = new StackPanel { MaxWidth = 640, Margin = new Thickness(32, 24, 32, 24) };
        var back = LaunchButton("タイトルに戻る", () => ShowTitle(hasSave));
        back.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(back);
        panel.Children.Add(Label("スコアランキング", 34, White, true));

        var state = game.S;
        var summary = state.Scores.FirstOrDefault() is ScoreEntry best
            ? $"最高スコア {best.Points:N0}"
            : "スコアはまだありません。";
        panel.Children.Add(Label(summary, 24, Gold, true));
        if (state.BestRound > 0)
            panel.Children.Add(Label($"最高到達 R{state.BestRound}  /  クリア {state.Clears}回", 15, Muted));
        panel.Children.Add(Label("このPCで終了したプレイの上位10件", 15, Muted));
        var rule = Label("到達ラウンド × 1,000 ＋ 残りHP ＋ クリア加点 1,000", 14, Muted);
        rule.Margin = new Thickness(0, 4, 0, 12);
        panel.Children.Add(rule);

        for (int index = 0; index < state.Scores.Count; index++)
        {
            var score = state.Scores[index];
            string mode = score.ChallengeNumber is int seed ? $"挑戦番号 {seed}" : "通常プレイ";
            string result = score.Cleared ? "クリア" : $"R{score.Round}";
            var card = Plate("#E3222C42", "#596680");
            card.Margin = new Thickness(0, 0, 0, 6);
            card.Padding = new Thickness(12, 8, 12, 8);
            card.Child = Label($"{index + 1}位   {score.Points:N0} pt\n{result}  /  HP {score.Hp}  /  {mode}", 16, index == 0 ? Gold : White);
            panel.Children.Add(card);
        }

        launchHost.Children.Add(new ScrollViewer
        {
            Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        });
        Keyboard.Focus(back);
    }

    Button LaunchButton(string title, Action action, bool primary = false)
    {
        var button = Btn(title, action, primary: primary);
        button.Height = 46;
        button.Margin = new Thickness(0, 8, 0, 0);
        button.Content = Label(title, 18, primary ? B("#14213A") : White, primary);
        return button;
    }
}
