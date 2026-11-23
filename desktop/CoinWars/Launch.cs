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
    DateTime? lastSavedAt;

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
            menu.Children.Add(Label($"ラウンド {save.Round} / 20  ·  HP {save.Hp}  /  {DifficultyName(save.Difficulty)}{challenge}", 15, Gold));
            if (!hasResult) AddResumeInfo(menu);
        }
        if (launchNotice is string notice)
            menu.Children.Add(Label(notice, 15, Gold));

        var difficulty = AddDifficultySelector(menu, save.Difficulty, "新しい挑戦の難易度");
        var start = LaunchButton("はじめから", () =>
        {
            bool hasExistingFile = hasSave || File.Exists(savePath) || File.Exists(savePath + ".bak");
            if (hasExistingFile &&
                MessageBox.Show(this, "現在の進行を置き換えて、新しいゲームを始めますか？",
                    "はじめから", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            EnterGame(true, SelectedDifficulty(difficulty));
        }, !hasSave);
        var resume = LaunchButton(hasResult ? "前回の結果を見る" : "つづきから", () => EnterGame(false), true);
        if (hasSave) menu.Children.Add(resume);
        menu.Children.Add(start);
        menu.Children.Add(LaunchButton("セーブ一覧", () => ShowSaveHistory(hasSave)));
        menu.Children.Add(LaunchButton("リザルト一覧", () => ShowRunResults(hasSave)));
        menu.Children.Add(LaunchButton("スコアランキング", () => ShowScores(hasSave)));
        var updateButton = LaunchButton("ゲームの更新", () => ShowUpdates(hasSave));
        menu.Children.Add(updateButton);
        SetUpdateButtonLabel(updateButton);
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
        panel.Children.Add(Label($"{RunMode(state.ChallengeNumber)} / {DifficultyName(state.Difficulty)}", 15, Muted));
        AddResumeInfo(panel);
        panel.Children.Add(LaunchButton("つづきから", () => EnterGame(false), true));
        AddBattleHistory(panel, state.BattleSaves);
    }

    void AddResumeInfo(StackPanel panel)
    {
        var state = game.S;
        string stage = state.Phase == "event" ? "投資イベント" : "編成準備";
        panel.Children.Add(Label($"R{state.Round}の{stage}から再開", 15, White));
        if (lastSavedAt is DateTime savedAt)
            panel.Children.Add(Label($"保存日時 {savedAt:yyyy/MM/dd HH:mm}", 14, Muted));
    }

    void ShowRunResults(bool hasSave, int? challengeFilter = null)
    {
        var panel = OpenHistoryPage("リザルト一覧", "タイトルに戻る", () => ShowTitle(hasSave));
        if (game.S.RunResults.Count == 0)
        {
            panel.Children.Add(Label("終了した挑戦はまだありません。", 18, Muted));
            return;
        }

        var numbers = game.S.RunResults.Select(run => run.ChallengeNumber).OfType<int>().Distinct().Order();
        var filter = new ComboBox { FontSize = 16 * textScale, Margin = new Thickness(0, 4, 0, 12) };
        panel.Children.Add(Label("チャレンジ番号で絞り込む", 15, Muted));
        panel.Children.Add(filter);
        var all = new ComboBoxItem { Content = "すべての挑戦" };
        filter.Items.Add(all);
        foreach (int number in numbers)
            filter.Items.Add(new ComboBoxItem { Content = RunMode(number), Tag = number });
        filter.SelectedItem = filter.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, challengeFilter)) ?? all;

        var results = new StackPanel();
        panel.Children.Add(results);
        void UpdateResults()
        {
            int? selectedNumber = (filter.SelectedItem as ComboBoxItem)?.Tag as int?;
            results.Children.Clear();
            foreach (var run in game.S.RunResults.AsEnumerable().Reverse())
            {
                if (selectedNumber.HasValue && run.ChallengeNumber != selectedNumber) continue;
                string summary = $"挑戦 {run.Attempt}   {RunOutcome(run.Outcome)}   R{run.Round} / 20   HP {run.Hp}\n{RunMode(run.ChallengeNumber)} / {DifficultyName(run.Difficulty)}   {RunScore(run)}";
                results.Children.Add(LaunchButton(summary, () => ShowRunDetails(hasSave, run, selectedNumber), height: 62));
            }
        }
        filter.SelectionChanged += (_, _) => UpdateResults();
        UpdateResults();
    }

    void ShowRunDetails(bool hasSave, RunResult run, int? challengeFilter)
    {
        var panel = OpenHistoryPage($"挑戦 {run.Attempt}   {RunOutcome(run.Outcome)}", "リザルト一覧に戻る", () => ShowRunResults(hasSave, challengeFilter));
        AddRunDetails(panel, run);
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
    static string RunScore(RunResult run) => run.Outcome == "abandoned"
        ? "スコアなし"
        : $"{new ScoreEntry(run.Round, run.Hp, run.Outcome == "clear", run.ChallengeNumber).Points:N0} pt";

    static void AddBattleHistory(StackPanel panel, List<BattleSave> battles)
    {
        var heading = Label("戦闘ごとの記録", 21, White, true);
        heading.Margin = new Thickness(0, 18, 0, 6);
        panel.Children.Add(heading);
        if (battles.Count == 0)
            panel.Children.Add(Label("完了した戦闘はまだありません。", 16, Muted));
        foreach (var battle in battles.AsEnumerable().Reverse())
        {
            string goldText = battle.Gold is int gold ? $"{gold} コイン" : "コインの記録なし";
            var card = Plate("#E3222C42", "#596680");
            card.Margin = new Thickness(0, 0, 0, 6);
            card.Padding = new Thickness(12, 8, 12, 8);
            var line = new TextBlock { FontSize = 16 * textScale, Foreground = White, TextWrapping = TextWrapping.Wrap };
            line.Inlines.Add(new System.Windows.Documents.Run($"R{battle.Round}   ") { FontWeight = FontWeights.SemiBold });
            line.Inlines.Add(new System.Windows.Documents.Run(OutcomeName(battle.Outcome))
            {
                FontWeight = FontWeights.SemiBold,
                Foreground = battle.Outcome switch { "win" => Cyan, "lose" => Loss, _ => Muted }
            });
            line.Inlines.Add(new System.Windows.Documents.Run($"   {BattleHp(battle)}   {goldText}"));
            card.Child = new Expander { Header = line, Content = BattleReportContent(battle), Foreground = White };
            panel.Children.Add(card);
        }
    }

    static string DifficultyName(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => "やさしい",
        Difficulty.Hard => "むずかしい",
        _ => "標準"
    };

    ComboBox AddDifficultySelector(StackPanel panel, Difficulty selected, string label, bool showStrength = true)
    {
        panel.Children.Add(Label(label, 15, White));
        var selector = new ComboBox { MinHeight = 32, Margin = new Thickness(0, 4, 0, 8) };
        foreach (var difficulty in new[] { Difficulty.Easy, Difficulty.Standard, Difficulty.Hard })
        {
            string strength = difficulty switch
            {
                Difficulty.Easy => "（敵のHP・攻撃 −20%）",
                Difficulty.Hard => "（敵のHP・攻撃 ＋20%）",
                _ => ""
            };
            // Keep the enum as the selection value and give the player a localized label.
            selector.Items.Add(new ComboBoxItem { Content = DifficultyName(difficulty) + (showStrength ? strength : ""), Tag = difficulty });
        }
        selector.SelectedItem = selector.Items.OfType<ComboBoxItem>().First(item => Equals(item.Tag, selected));
        System.Windows.Automation.AutomationProperties.SetName(selector, label);
        panel.Children.Add(selector);
        return selector;
    }

    static Difficulty SelectedDifficulty(ComboBox selector) => (Difficulty)((ComboBoxItem)selector.SelectedItem).Tag;

    void ShowScores(bool hasSave, Difficulty? selectedDifficulty = null)
    {
        launchHost.Children.Clear();
        launchHost.Background = new LinearGradientBrush(ColorOf("#101725"), ColorOf("#344558"), 24);
        var panel = new StackPanel { MaxWidth = 640, Margin = new Thickness(32, 24, 32, 24) };
        var back = LaunchButton("タイトルに戻る", () => ShowTitle(hasSave));
        back.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(back);
        panel.Children.Add(Label("スコアランキング", 34, White, true));

        var state = game.S;
        var difficulty = AddDifficultySelector(panel, selectedDifficulty ?? state.Difficulty, "難易度", showStrength: false);
        difficulty.SelectionChanged += (_, _) => ShowScores(hasSave, SelectedDifficulty(difficulty));
        var scores = state.Scores.Where(entry => entry.Difficulty == SelectedDifficulty(difficulty))
            .OrderByDescending(entry => entry.Points).ToList();
        var summary = scores.FirstOrDefault() is ScoreEntry best
            ? $"最高スコア {best.Points:N0}"
            : "スコアはまだありません。";
        panel.Children.Add(Label(summary, 24, Gold, true));
        panel.Children.Add(Label("このPCで終了したプレイの上位10件", 15, Muted));
        var rule = Label("到達ラウンド × 1,000 ＋ 残りHP ＋ クリア加点 1,000", 14, Muted);
        rule.Margin = new Thickness(0, 4, 0, 12);
        panel.Children.Add(rule);

        for (int index = 0; index < scores.Count; index++)
        {
            var score = scores[index];
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
        Keyboard.Focus(selectedDifficulty.HasValue ? difficulty : back);
    }

    Button LaunchButton(string title, Action action, bool primary = false, double height = 46)
    {
        var button = Btn(title, action, primary: primary);
        button.Height = height;
        button.Margin = new Thickness(0, 8, 0, 0);
        button.Content = Label(title, 18, primary ? B("#14213A") : White, primary);
        return button;
    }
}
