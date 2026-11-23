using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace CoinWars;

public partial class GameWindow
{
    static readonly HttpClient updateHttp = new();
    static readonly ReleaseUpdates releaseUpdates = new(updateHttp);
    static readonly Version appVersion = typeof(GameWindow).Assembly.GetName().Version!;
    readonly CancellationTokenSource updateLifetime = new();
    Task<(ReleaseUpdate? Update, string? Error)>? updateCheck;

    async Task<(ReleaseUpdate? Update, string? Error)> CheckForUpdateAsync()
    {
        try { return (await releaseUpdates.CheckAsync(appVersion, updateLifetime.Token), null); }
        catch (InvalidDataException ex) { return (null, ex.Message); }
        catch (Exception) { return (null, "更新を確認できませんでした。接続を確認して、もう一度お試しください。"); }
    }

    async void SetUpdateButtonLabel(Button button)
    {
        var result = await (updateCheck ??= CheckForUpdateAsync());
        if (result.Update != null)
            button.Content = Label($"更新あり  v{result.Update.Version}", 18, Gold, true);
    }

    void ShowUpdates(bool hasSave)
    {
        var panel = OpenHistoryPage("ゲームの更新", "タイトルに戻る", () => ShowTitle(hasSave));
        panel.Children.Add(Label($"現在のバージョン v{appVersion.ToString(3)}", 16, Muted));
        var status = Label("", 16, White);
        status.Margin = new Thickness(0, 10, 0, 12);
        panel.Children.Add(status);
        var actions = new StackPanel();
        panel.Children.Add(actions);
        ReleaseUpdate? available = null;
        var install = LaunchButton("ダウンロードして更新", async () =>
        {
            if (available is not ReleaseUpdate update) return;
            panel.IsEnabled = false;
            status.Text = $"v{update.Version}をダウンロードして検証しています…";
            string? error = await OpenUpdateInstallerAsync(update);
            if (error == null || updateLifetime.IsCancellationRequested) return;
            status.Text = error;
            panel.IsEnabled = true;
        }, true);
        install.Visibility = Visibility.Collapsed;
        actions.Children.Add(install);
        actions.Children.Add(LaunchButton("もう一度確認", () => Refresh(true)));

        async void Refresh(bool force)
        {
            install.Visibility = Visibility.Collapsed;
            actions.IsEnabled = false;
            status.Text = "GitHub Releasesで更新を確認しています…";
            if (force) updateCheck = CheckForUpdateAsync();
            var result = await (updateCheck ??= CheckForUpdateAsync());
            if (updateLifetime.IsCancellationRequested) return;
            available = result.Update;
            actions.IsEnabled = true;
            if (available == null)
            {
                status.Text = result.Error ?? "現在のバージョンで最新です。";
                return;
            }
            status.Text = $"v{available.Version}を利用できます。\nダウンロード後、インストーラーを開くとゲームが終了します。";
            install.Visibility = Visibility.Visible;
        }

        Refresh(false);
    }

    async Task<string?> OpenUpdateInstallerAsync(ReleaseUpdate update)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CoinWars-Update-" + Guid.NewGuid().ToString("N"));
        string installer = Path.Combine(directory, update.FileName);
        string partial = installer + ".download";
        bool started = false;
        try
        {
            Directory.CreateDirectory(directory);
            await releaseUpdates.DownloadAsync(update, partial, updateLifetime.Token);
            updateLifetime.Token.ThrowIfCancellationRequested();
            File.Move(partial, installer);
            using var process = Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true });
            if (process == null) return "インストーラーを開けませんでした。";
            started = true;
            Close();
            return null;
        }
        catch (InvalidDataException ex) { return ex.Message; }
        catch (Exception) { return "更新を開始できませんでした。もう一度お試しください。"; }
        finally
        {
            if (!started)
            {
                try { File.Delete(partial); File.Delete(installer); Directory.Delete(directory); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
