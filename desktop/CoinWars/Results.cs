using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CoinWars;

public partial class GameWindow
{
    static readonly Brush Loss = B("#E08AA0");

    void EndDialog()
    {
        var state = game.S;
        var run = state.RunResults.LastOrDefault() is RunResult last && last.Attempt == state.Plays ? last : null;
        bool abandoned = run?.Outcome == "abandoned";
        bool win = !abandoned && state.FinalScore().Cleared;
        int? challenge = state.ChallengeNumber;

        string title = win ? "TRIUMPH" : abandoned ? "途中終了" : "GAME OVER";
        string lead = win ? "全20ラウンド制覇" : abandoned ? $"R{state.Round}で挑戦を終了" : $"R{state.Round}で敗退";
        var panel = Modal(title, $"{lead}   ·   挑戦 {state.Plays}   ·   {DifficultyName(state.Difficulty)}   ·   {RunMode(challenge)}",
            900, 760, win ? Gold : abandoned ? Muted : Loss);

        var score = abandoned ? null : state.FinalScore();
        panel.Children.Add(StatTiles(state.Round, state.Hp, state.Gold, score?.Points));
        if (score != null && RankingLine(state, score) is TextBlock rank) panel.Children.Add(rank);

        var battles = run?.Battles ?? state.BattleSaves;
        panel.Children.Add(Section("ラウンド推移"));
        panel.Children.Add(RoundStrip(battles));
        panel.Children.Add(Tally(battles));

        var team = state.Board.OfType<Unit>().ToList();
        if (team.Count > 0)
        {
            panel.Children.Add(Section("最終編成"));
            panel.Children.Add(TeamRow(team));
        }

        var records = Label($"自己ベスト R{state.BestRound}   ·   クリア {state.Clears}回   ·   挑戦 {state.Plays}回", 14, Muted);
        records.Margin = new Thickness(0, 18, 0, 0);
        panel.Children.Add(records);

        // Next-run choices on the first row, review and exit on the second.
        var main = new UniformGrid { Rows = 1, Margin = new Thickness(0, 18, 0, 0) };
        if (challenge is int seed)
            main.Children.Add(ActionButton("同じ番号で再挑戦", () => RestartWith(seed, state.Difficulty), true));
        main.Children.Add(ActionButton(challenge.HasValue ? "通常プレイを始める" : "もう一度挑戦する", Restart, !challenge.HasValue));
        panel.Children.Add(main);
        var secondary = new UniformGrid { Rows = 1 };
        if (run != null) secondary.Children.Add(ActionButton("戦闘ごとの記録", () => SetDialog("runDetails")));
        secondary.Children.Add(ActionButton("タイトルへ戻る", ReturnToTitle));
        panel.Children.Add(secondary);
    }

    Button ActionButton(string text, Action action, bool primary = false)
    {
        var button = Btn(text, action, true, primary);
        button.Content = Label(text, 17, primary ? B("#14213A") : White, primary);
        button.MinHeight = 52;
        button.Margin = new Thickness(5);
        return button;
    }

    static TextBlock? RankingLine(State state, ScoreEntry score)
    {
        var ranked = state.Scores.Where(entry => entry.Difficulty == score.Difficulty).ToList();
        if (!ranked.Contains(score)) return null;
        int rank = ranked.Count(entry => entry.Points > score.Points) + 1;
        string text = rank == 1
            ? $"★ {DifficultyName(score.Difficulty)}のランキング 1位   自己最高スコア"
            : $"{DifficultyName(score.Difficulty)}のランキング {rank}位";
        var label = Label(text, 17, Gold, rank == 1);
        label.Margin = new Thickness(5, 10, 0, 0);
        return label;
    }

    static UniformGrid StatTiles(int round, int hp, int gold, int? points)
    {
        var tiles = new UniformGrid { Columns = 4, Margin = new Thickness(0, 20, 0, 0) };
        tiles.Children.Add(StatTile("到達ラウンド", $"R{round}", "/ 20", White));
        tiles.Children.Add(StatTile("残りHP", hp.ToString(), "", hp > 0 ? Cyan : Loss));
        tiles.Children.Add(StatTile("所持コイン", gold.ToString(), "", Gold));
        tiles.Children.Add(points is int value
            ? StatTile("スコア", value.ToString("N0"), "pt", Gold)
            : StatTile("スコア", "—", "記録なし", Muted));
        return tiles;
    }

    static Border StatTile(string caption, string value, string unit, Brush color)
    {
        var stack = new StackPanel();
        stack.Children.Add(Label(caption, 13, Muted));
        var line = new TextBlock { Margin = new Thickness(0, 2, 0, 0) };
        line.Inlines.Add(new System.Windows.Documents.Run(value) { FontSize = 30 * textScale, FontWeight = FontWeights.SemiBold, Foreground = color });
        if (unit.Length > 0)
            line.Inlines.Add(new System.Windows.Documents.Run(" " + unit) { FontSize = 14 * textScale, Foreground = Muted });
        stack.Children.Add(line);
        var tile = Plate("#B3141C30", "#465372");
        tile.Margin = new Thickness(5);
        tile.Padding = new Thickness(14, 10, 14, 12);
        tile.Child = stack;
        return tile;
    }

    static TextBlock Section(string text)
    {
        var label = Label(text, 18, White, true);
        label.Margin = new Thickness(5, 22, 0, 8);
        return label;
    }

    static string OutcomeName(string outcome) => outcome switch { "win" => "勝利", "lose" => "敗北", "draw" => "引き分け", _ => "結果不明" };

    static UniformGrid RoundStrip(List<BattleSave> battles)
    {
        var byRound = battles.GroupBy(battle => battle.Round).ToDictionary(group => group.Key, group => group.Last());
        var strip = new UniformGrid { Columns = 20, Margin = new Thickness(3, 0, 3, 0) };
        for (int round = 1; round <= 20; round++)
        {
            byRound.TryGetValue(round, out var battle);
            string fill = battle?.Outcome switch { "win" => "#2F7F86", "lose" => "#8A4459", "draw" => "#5D6685", null => "#161E31", _ => "#3A4257" };
            bool boss = round % 5 == 0;
            var cell = new Border
            {
                Background = B(fill), BorderBrush = boss ? Gold : B("#2C3650"), BorderThickness = new Thickness(boss ? 1.5 : 1),
                Margin = new Thickness(2), MinHeight = 34, CornerRadius = new CornerRadius(2),
                Child = new TextBlock
                {
                    Text = round.ToString(), FontSize = 13 * textScale, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, Foreground = battle == null ? B("#56617C") : White
                },
                ToolTip = battle == null ? $"R{round}  未到達" : $"R{round}  {OutcomeName(battle.Outcome)}   {BattleHp(battle)}"
            };
            strip.Children.Add(cell);
        }
        return strip;
    }

    static WrapPanel Tally(List<BattleSave> battles)
    {
        var row = new WrapPanel { Margin = new Thickness(5, 8, 0, 0) };
        void Add(string text, string swatch)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 20, 0) };
            item.Children.Add(new Border { Width = 12, Height = 12, Background = B(swatch), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            item.Children.Add(Label(text, 14, Muted));
            row.Children.Add(item);
        }
        Add($"勝利 {battles.Count(b => b.Outcome == "win")}", "#2F7F86");
        Add($"敗北 {battles.Count(b => b.Outcome == "lose")}", "#8A4459");
        Add($"引き分け {battles.Count(b => b.Outcome == "draw")}", "#5D6685");
        var boss = Label("金枠はボスラウンド", 14, Muted);
        row.Children.Add(boss);
        return row;
    }

    static WrapPanel TeamRow(List<Unit> team)
    {
        var row = new WrapPanel { Margin = new Thickness(3, 0, 0, 0) };
        foreach (var unit in team)
        {
            var def = Data.Def(unit);
            var card = new Grid { Width = 92, Height = 112, Margin = new Thickness(2) };
            card.Children.Add(new Border { Background = B("#B3141C30"), BorderBrush = B(CostColors[Math.Clamp(def.Cost, 1, 5)]), BorderThickness = new Thickness(1) });
            var portrait = Portrait(unit.Key);
            portrait.Margin = new Thickness(4, 4, 4, 30);
            card.Children.Add(portrait);
            var stars = Label(new string('★', unit.Star), 11, Gold, true);
            stars.VerticalAlignment = VerticalAlignment.Top;
            stars.Margin = new Thickness(7, 4, 0, 0);
            card.Children.Add(stars);
            var name = Label(def.Name, 12, White, true);
            name.TextWrapping = TextWrapping.NoWrap;
            name.TextAlignment = TextAlignment.Center;
            name.VerticalAlignment = VerticalAlignment.Bottom;
            name.Margin = new Thickness(4, 0, 4, 7);
            card.Children.Add(name);
            row.Children.Add(card);
        }
        return row;
    }

    static string BattleHp(BattleSave battle) => battle.Hp is int hp
        ? battle.HpBefore is int before ? $"HP {before} → {hp}（{hp - before:+0;-0;0}）" : $"HP {hp}"
        : "HPの記録なし";

    static void AddRunDetails(StackPanel panel, RunResult run)
    {
        panel.Children.Add(Label($"{RunMode(run.ChallengeNumber)}   ·   {DifficultyName(run.Difficulty)}", 16, Muted));
        panel.Children.Add(StatTiles(run.Round, run.Hp, run.Gold,
            run.Outcome == "abandoned" ? null : new ScoreEntry(run.Round, run.Hp, run.Outcome == "clear", run.ChallengeNumber, run.Difficulty).Points));
        panel.Children.Add(Section("ラウンド推移"));
        panel.Children.Add(RoundStrip(run.Battles));
        panel.Children.Add(Tally(run.Battles));
        AddBattleHistory(panel, run.Battles);
    }
}
