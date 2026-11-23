using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace CoinWars;

public partial class GameWindow
{
    readonly PresentationTime presentationTime = new();
    readonly List<AnimationClock> animationClocks = [];
    Button? pauseButton, speedButton;
    TextBlock? battleStatus;
    bool Planning => game.S.Phase == "plan" && !animating && !resultPending;
    bool ShowingBattle => game.S.Phase == "battle" || resultPending;
    bool Suspended => PresentationTime.ShouldSuspend(paused, dialog != null);

    void Animate(DependencyObject target, DependencyProperty property, AnimationTimeline timeline)
    {
        var clock = (AnimationClock)timeline.CreateClock(true);
        animationClocks.Add(clock);
        clock.Completed += (_, _) => animationClocks.Remove(clock);
        if (target is UIElement element) element.ApplyAnimationClock(property, clock);
        else if (target is Animatable animatable) animatable.ApplyAnimationClock(property, clock);
        else throw new InvalidOperationException("Unsupported animation target");
        clock.Controller!.SpeedRatio = speed;
        if (Suspended) clock.Controller.Pause();
    }
    void SyncPlayback()
    {
        presentationTime.Speed = speed;
        presentationTime.Suspended = Suspended;
        foreach (var clock in animationClocks.ToArray())
        {
            clock.Controller!.SpeedRatio = speed;
            if (Suspended) clock.Controller.Pause(); else clock.Controller.Resume();
        }
        if (Suspended) GameAudio.Stop();
        if (battleStatus != null && ShowingBattle) battleStatus.Text = Suspended ? "PAUSED" : "AUTO BATTLE";
        if (pauseButton != null) pauseButton.Content = Label(paused ? "▶ 再開" : "Ⅱ 停止", 14, White);
        if (speedButton != null) speedButton.Content = Label($"×{speed}", 14, White);
    }
    async Task<bool> WaitPresentation(double milliseconds, int generation)
    {
        double end = presentationTime.Milliseconds + milliseconds;
        while (generation == transitionGeneration)
        {
            if (!Suspended && presentationTime.Milliseconds >= end) return true;
            await Task.Delay(8);
        }
        return false;
    }
    void TogglePause()
    {
        if (!ShowingBattle) return;
        paused = !paused;
        SyncPlayback();
    }
    void CycleSpeed()
    {
        speed = speed == 1 ? 2 : speed == 2 ? 4 : 1;
        SyncPlayback();
    }
    void ToggleMute()
    {
        preferences = preferences with { Muted = !preferences.Muted };
        if (preferences.Muted) GameAudio.Stop();
        SavePreferences();
        if (dialog == "settings") RenderOverlays();
    }
    void SetDialog(string? value)
    {
        dialog = value;
        SyncPlayback();
        RenderOverlays();
    }
    void RenderOverlays()
    {
        overlays.Children.Clear();
        if (dialog == "runDetails" && game.S.RunResults.LastOrDefault() is RunResult run)
        {
            var panel = Modal($"挑戦 {run.Attempt}   {RunOutcome(run.Outcome)}", "", 820, 620);
            panel.Children.Add(Btn("終了画面に戻る [Esc]", () => SetDialog(null)));
            AddRunDetails(panel, run);
        }
        else
        {
            if (!resultPending && game.S.Phase == "event") EventDialog();
            if (!resultPending && game.S.Phase == "over") EndDialog();
            if (dialog != null) UtilityDialog();
        }
        modalHost.IsHitTestVisible=overlays.Children.Count>0;
        modalHost.Background=overlays.Children.Count>0?System.Windows.Media.Brushes.Transparent:null;
    }
    void ClearPresentation()
    {
        foreach (var clock in animationClocks.ToArray()) clock.Controller?.Remove();
        animationClocks.Clear();
        effects.Children.Clear();
        scene.RenderTransform = null;
        GameAudio.Stop();
    }
}
