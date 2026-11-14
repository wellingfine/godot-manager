using System;

namespace GodotManager.Models;

public sealed class GodotRelease
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Flavor { get; init; }
    public string Channel { get; init; } = "stable";
    public DateTime? ReleaseDate { get; init; }
    public string DownloadPageUrl { get; init; } = string.Empty;
    public string NewsUrl { get; init; } = string.Empty;

    /// <summary>由 ViewModel 根据「.NET 版」勾选状态设置，供列表项显示 .NET 标签。</summary>
    public bool ShowDotNetTag { get; set; }

    public bool IsStable => Channel == "stable";
    public string DateText => ReleaseDate?.ToString("yyyy-MM-dd") ?? string.Empty;
}
