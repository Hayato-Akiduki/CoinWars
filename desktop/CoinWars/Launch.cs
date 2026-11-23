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
        menu.Children.Add(LaunchButton("終了", Close));

        launchHost.Children.Add(new ScrollViewer
        {
            Content = menu, VerticalAlignment = VerticalAlignment.Center,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });
        Keyboard.Focus(hasSave ? resume : start);
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
