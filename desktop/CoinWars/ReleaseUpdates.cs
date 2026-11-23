using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace CoinWars;

internal record ReleaseUpdate(Version Version, string FileName, long Size, string Sha256);

internal sealed class ReleaseUpdates(HttpClient http)
{
    const string Repository = "Hayato-Akiduki/CoinWars";
    const long MaxInstallerBytes = 512L * 1024 * 1024;
    static readonly Uri LatestRelease = new($"https://api.github.com/repos/{Repository}/releases/latest");

    public async Task<ReleaseUpdate?> CheckAsync(Version current, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestRelease);
        request.Headers.UserAgent.ParseAdd("CoinWars/desktop");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token).ConfigureAwait(false);
        var release = document.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;

        string? tag = release.GetProperty("tag_name").GetString();
        if (tag == null || !tag.StartsWith('v') || !Version.TryParse(tag[1..], out var version) ||
            version.Build < 0 || version.Revision >= 0 || tag != $"v{version}")
            throw new InvalidDataException("公開バージョンを確認できませんでした。");
        if (version <= new Version(current.Major, current.Minor, Math.Max(0, current.Build))) return null;

        string fileName = $"CoinWars-Setup-v{version}.exe";
        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != fileName) continue;
            long size = asset.GetProperty("size").GetInt64();
            string? digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
            if (size <= 0 || size > MaxInstallerBytes || digest == null || digest.Length != 71 ||
                !digest.StartsWith("sha256:", StringComparison.Ordinal) || !digest[7..].All(Uri.IsHexDigit))
                break;
            return new(version, fileName, size, digest[7..]);
        }

        throw new InvalidDataException("更新に必要なインストーラー情報が見つかりませんでした。");
    }

    public async Task DownloadAsync(ReleaseUpdate update, string path, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        var uri = new Uri($"https://github.com/{Repository}/releases/download/v{update.Version}/{update.FileName}");
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) != 0)
        {
            received += read;
            if (received > update.Size) throw new InvalidDataException("インストーラーのサイズが配布情報と一致しません。");
            hash.AppendData(buffer, 0, read);
            await file.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
        }
        if (received != update.Size || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(update.Sha256)))
            throw new InvalidDataException("インストーラーの検証に失敗しました。");
    }
}
