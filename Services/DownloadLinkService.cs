using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GodotManager.Models;

namespace GodotManager.Services;

/// <summary>
/// 从官网版本下载页的「Supported platforms」区域抓取下载按钮的真实 href，
/// 不在本地拼 downloads.godotengine.org 的参数，官网页面怎么渲染就怎么用。
/// </summary>
public sealed class DownloadLinkService
{
    private static readonly HttpClient Http = CreateClient();

    private static readonly Regex PlatformBlockRegex = new(
        @"<div class=""download-platform"">\s*<img[^>]*title=""(?<platform>[^""]+)""[^>]*>\s*[^<]*</div>\s*" +
        @"(?<buttons>(?:<a\s[^>]*class=""btn btn-download[^""]*""[^>]*>.*?</a>\s*)+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex ButtonRegex = new(
        @"<a\s+(?=[^>]*href=""(?<href>[^""]+)"")(?=[^>]*class=""(?<class>[^""]+)"")[^>]*>.*?</a>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GodotManager/1.0");
        return client;
    }

    /// <summary>归档页解析出的 URL 可能没有尾部斜杠，统一补成官网规范形式，也用于失败详情展示。</summary>
    public static string GetPageUrl(GodotRelease release) =>
        string.IsNullOrWhiteSpace(release.DownloadPageUrl)
            ? $"https://godotengine.org/download/archive/{Uri.EscapeDataString(release.Id)}/"
            : release.DownloadPageUrl.TrimEnd('/') + "/";

    public async Task<string> GetDownloadUrlAsync(GodotRelease release, bool mono, ShellPlatform platform,
        CancellationToken cancellationToken = default)
    {
        var pageUrl = GetPageUrl(release);
        var html = await Http.GetStringAsync(pageUrl, cancellationToken);
        return Parse(html, mono, platform)
               ?? throw new InvalidOperationException(
                   $"在 {pageUrl} 上未找到 {PlatformService.GetPagePlatformName(platform)} 的 " +
                   $"{(mono ? ".NET" : "Standard")} 下载按钮，官网页面结构可能已变更。");
    }

    /// <summary>
    /// 从页面 HTML 中定位目标平台分组，再按 .NET / Standard 按钮取 href。
    /// </summary>
    public static string? Parse(string html, bool mono, ShellPlatform platform)
    {
        var platformName = PlatformService.GetPagePlatformName(platform);
        foreach (Match block in PlatformBlockRegex.Matches(html))
        {
            if (!string.Equals(block.Groups["platform"].Value, platformName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (Match button in ButtonRegex.Matches(block.Groups["buttons"].Value))
            {
                var isMono = button.Groups["class"].Value.Contains("btn-download-primary--mono",
                    StringComparison.OrdinalIgnoreCase);
                if (isMono == mono)
                {
                    return button.Groups["href"].Value;
                }
            }
        }

        return null;
    }
}
