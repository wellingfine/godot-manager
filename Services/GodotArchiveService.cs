using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GodotManager.Models;

namespace GodotManager.Services;

public static class GodotArchiveService
{
    public const string ArchiveUrl = "https://godotengine.org/download/archive/";

    private static readonly HttpClient Http = CreateClient();

    private static readonly Regex ReleaseRegex = new(
        @"<a class=""archive-version""[^>]*href=""(?<href>/download/archive/[^""]+)""[^>]*>\s*<h4[^>]*>(?<id>[^<]+)</h4>\s*<p[^>]*>\s*<span>(?<date>[^<]+)</span>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex SlugRegex = new(
        @"^(?<version>\d+(?:\.\d+)*)-(?<flavor>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ChannelRegex = new(@"^(?<channel>[a-z]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NewsRegex = new(
        @"<a[^>]*class=""[^""]*btn-release-notes[^""]*""[^>]*href=""(?<href>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GodotManager/1.0");
        return client;
    }

    public static async Task<IReadOnlyList<GodotRelease>> FetchAsync(CancellationToken cancellationToken = default)
    {
        var html = await Http.GetStringAsync(ArchiveUrl, cancellationToken);
        return Parse(html);
    }

    public static IReadOnlyList<GodotRelease> Parse(string html)
    {
        var result = new List<GodotRelease>();

        var matches = ReleaseRegex.Matches(html);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var href = match.Groups["href"].Value;
            var id = match.Groups["id"].Value.Trim();
            var slug = href.TrimEnd('/').Split('/').LastOrDefault() ?? id;
            var slugMatch = SlugRegex.Match(slug);
            if (!slugMatch.Success)
            {
                continue;
            }

            var version = slugMatch.Groups["version"].Value;
            var flavor = slugMatch.Groups["flavor"].Value.ToLowerInvariant();
            var channel = ChannelRegex.Match(flavor).Groups["channel"].Value.ToLowerInvariant();

            var segmentStart = match.Index + match.Length;
            var segmentEnd = i + 1 < matches.Count ? matches[i + 1].Index : html.Length;
            var newsMatch = NewsRegex.Match(html, segmentStart, Math.Max(0, segmentEnd - segmentStart));
            var newsUrl = newsMatch.Success
                ? new Uri(new Uri("https://godotengine.org"), newsMatch.Groups["href"].Value).ToString()
                : string.Empty;

            result.Add(new GodotRelease
            {
                Id = id,
                Version = version,
                Flavor = flavor,
                Channel = channel,
                ReleaseDate = ParseDate(match.Groups["date"].Value),
                DownloadPageUrl = new Uri(new Uri("https://godotengine.org"), href).ToString(),
                NewsUrl = newsUrl
            });
        }

        return result
            .DistinctBy(release => release.Id, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(release => release.Version, GodotVersionComparer.Instance)
            .ThenByDescending(release => release.ReleaseDate ?? DateTime.MinValue)
            .ThenBy(release => release.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DateTime? ParseDate(string text)
    {
        if (DateTime.TryParseExact(text.Trim(), "d MMMM yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        return DateTime.TryParse(text.Trim(), CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}

/// <summary>
/// 语义化版本号比较器：4.10 &gt; 4.9 &gt; 4.7.2 &gt; 4.7 &gt; 3.6。
/// </summary>
public sealed class GodotVersionComparer : IComparer<string>
{
    public static GodotVersionComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        var left = Parse(x);
        var right = Parse(y);

        for (var index = 0; index < Math.Max(left.Length, right.Length); index++)
        {
            var leftPart = index < left.Length ? left[index] : 0;
            var rightPart = index < right.Length ? right[index] : 0;
            if (leftPart != rightPart)
            {
                return leftPart < rightPart ? -1 : 1;
            }
        }

        return 0;
    }

    public static int GetMajor(string? version) => Parse(version).FirstOrDefault();

    private static int[] Parse(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return Array.Empty<int>();
        }

        return version.Split('.')
            .Select(part => int.TryParse(part, out var value) ? value : 0)
            .ToArray();
    }
}
