using System.Globalization;
using System.Text.RegularExpressions;
using AbsoluteBot.Services.UtilityServices;
using Newtonsoft.Json.Linq;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AbsoluteBot.Services.MediaServices;

/// <summary>
///     Один кадр живого эфира Twitch: последний закрытый сегмент около 720p и разовый ffmpeg.
/// </summary>
public sealed partial class TwitchFrameGrabber(ConfigService configService) : IDisposable
{
    private const string WebClientId = "kimne78kx3ncx6brgo4mv6wki5h1gway";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public void Dispose() => _http.Dispose();

    public async Task<string?> CaptureJpegBase64Async(CancellationToken cancellationToken)
    {
        var channel = await configService.GetConfigValueAsync<string>("TwitchChannelName").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(channel)) return null;

        var directory = Path.Combine(Path.GetTempPath(), "absolutebot-frame-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var segmentPath = Path.Combine(directory, "segment.ts");
        var jpegPath = Path.Combine(directory, "frame.jpg");
        try
        {
            var token = await PlaybackTokenAsync(channel, cancellationToken).ConfigureAwait(false);
            if (token == null) return null;

            var master = await GetStringAsync(UsherUrl(channel, token.Value.Value, token.Value.Signature), cancellationToken)
                .ConfigureAwait(false);
            var variant = PickVariant(master);
            if (variant == null) return null;

            var media = await GetStringAsync(variant, cancellationToken).ConfigureAwait(false);
            var segmentUrl = LastSegment(media);
            if (segmentUrl == null) return null;

            var bytes = await _http.GetByteArrayAsync(segmentUrl, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(segmentPath, bytes, cancellationToken).ConfigureAwait(false);
            if (!await RunFfmpegAsync(segmentPath, jpegPath, cancellationToken).ConfigureAwait(false)) return null;
            if (!File.Exists(jpegPath)) return null;
            var jpeg = await File.ReadAllBytesAsync(jpegPath, cancellationToken).ConfigureAwait(false);
            if (jpeg.Length == 0) return null;
            jpeg = ShrinkIfNeeded(jpeg);
            return Convert.ToBase64String(jpeg);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            Log.Warning(ex, "Кадр стрима Twitch не скачался.");
            return null;
        }
        finally
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Временный кадр не удалился.");
            }
        }
    }

    private async Task<(string Value, string Signature)?> PlaybackTokenAsync(string channel, CancellationToken cancellationToken)
    {
        var payload = new JObject
        {
            ["query"] =
                "query PlaybackAccessToken($login: String!, $playerType: String!) { streamPlaybackAccessToken(channelName: $login, params: {platform: \"web\", playerBackend: \"mediaplayer\", playerType: $playerType}) { value signature } }",
            ["variables"] = new JObject { ["login"] = channel, ["playerType"] = "embed" }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gql.twitch.tv/gql");
        request.Headers.TryAddWithoutValidation("Client-ID", WebClientId);
        request.Content = new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        var json = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var token = json["data"]?["streamPlaybackAccessToken"];
        var value = token?["value"]?.ToString();
        var signature = token?["signature"]?.ToString();
        return string.IsNullOrEmpty(value) || string.IsNullOrEmpty(signature) ? null : (value, signature);
    }

    private static string UsherUrl(string channel, string token, string signature) =>
        "https://usher.ttvnw.net/api/channel/hls/" + Uri.EscapeDataString(channel) +
        ".m3u8?client_id=" + WebClientId +
        "&token=" + Uri.EscapeDataString(token) +
        "&sig=" + Uri.EscapeDataString(signature) +
        "&allow_source=true&allow_audio_only=false";

    private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Client-ID", WebClientId);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static string? PickVariant(string playlist)
    {
        string? best = null;
        var bestScore = int.MaxValue;
        string? pending = null;
        foreach (var raw in playlist.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("#EXT-X-STREAM-INF", StringComparison.Ordinal))
            {
                pending = line;
                continue;
            }

            if (pending == null || line.Length == 0 || line.StartsWith('#')) continue;
            var info = pending;
            pending = null;
            if (info.Contains("audio_only", StringComparison.OrdinalIgnoreCase)) continue;
            var resolution = Resolution().Match(info);
            if (!resolution.Success) continue;
            if (!int.TryParse(resolution.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
                continue;
            if (height is < 360 or > 1080) continue;
            var score = Math.Abs(height - 720);
            if (score < bestScore)
            {
                bestScore = score;
                best = line;
            }
        }

        return best;
    }

    internal static string? LastSegment(string playlist)
    {
        string? last = null;
        foreach (var raw in playlist.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                last = line;
        }

        return last;
    }

    private static async Task<bool> RunFfmpegAsync(string segmentPath, string jpegPath, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add("-y");
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(segmentPath);
            process.StartInfo.ArgumentList.Add("-frames:v");
            process.StartInfo.ArgumentList.Add("1");
            process.StartInfo.ArgumentList.Add("-q:v");
            process.StartInfo.ArgumentList.Add("3");
            process.StartInfo.ArgumentList.Add(jpegPath);
            if (!process.Start()) return false;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch (Exception) { /* процесс уже завершился */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Warning(ex, "ffmpeg для кадра стрима недоступен.");
            return false;
        }
    }

    private static byte[] ShrinkIfNeeded(byte[] jpeg)
    {
        if (jpeg.Length <= 1_500_000) return jpeg;
        using var image = Image.Load(jpeg);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(1280, 720)
        }));
        using var output = new MemoryStream();
        image.Save(output, new JpegEncoder { Quality = 75 });
        return output.ToArray();
    }

    [GeneratedRegex(@"RESOLUTION=(\d+)x(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex Resolution();
}
