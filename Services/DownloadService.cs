using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GodotManager.Models;

namespace GodotManager.Services;

public sealed record DownloadProgress(long Received, long? Total)
{
    public double Percent => Total is > 0 ? Math.Min(100, Received * 100d / Total.Value) : 0;
    public string Text => Total is > 0
        ? $"{Received / 1048576d:F1} / {Total.Value / 1048576d:F1} MB"
        : $"{Received / 1048576d:F1} MB";
}

public sealed class DownloadService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GodotManager/1.0");
        return client;
    }

    public static string BuildDownloadUrl(GodotRelease release, bool mono)
    {
        var (slug, platform) = PlatformService.GetDownloadSpec(mono);
        return "https://downloads.godotengine.org/?version=" + Uri.EscapeDataString(release.Version) +
               "&flavor=" + Uri.EscapeDataString(release.Flavor) +
               "&slug=" + Uri.EscapeDataString(slug) +
               "&platform=" + Uri.EscapeDataString(platform);
    }

    public async Task DownloadAsync(string url, string destination, IProgress<DownloadProgress> progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long received = 0;
        long lastReport = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;

            if (received - lastReport >= 512 * 1024)
            {
                lastReport = received;
                progress.Report(new DownloadProgress(received, total));
            }
        }

        progress.Report(new DownloadProgress(received, total));
    }
}
