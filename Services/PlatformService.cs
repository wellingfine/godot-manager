using System;
using GodotManager.Models;

namespace GodotManager.Services;

/// <summary>
/// 平台相关的命名与转换。下载目标平台由用户在界面选择，可以和运行系统不同；
/// <see cref="Current"/> 表示当前运行系统，用于默认目标平台和启动判断。
/// </summary>
public static class PlatformService
{
    /// <summary>下拉顺序：Windows / macOS / Linux。</summary>
    public static readonly ShellPlatform[] All =
        { ShellPlatform.Windows, ShellPlatform.Mac, ShellPlatform.Linux };

    public static ShellPlatform Current => ShellService.Current;

    public static ShellPlatform FromIndex(int index) =>
        index >= 0 && index < All.Length ? All[index] : Current;

    public static int ToIndex(ShellPlatform platform)
    {
        var index = Array.IndexOf(All, platform);
        return index < 0 ? 0 : index;
    }

    /// <summary>state.json 里存枚举名；旧配置没有该字段或值无效时回退到当前系统。</summary>
    public static ShellPlatform Parse(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out ShellPlatform platform) ? platform : Current;

    /// <summary>官网下载页的平台分组标题，同时也是界面上的显示名。</summary>
    public static string GetPagePlatformName(ShellPlatform platform) => platform switch
    {
        ShellPlatform.Mac => "macOS",
        ShellPlatform.Linux => "Linux",
        _ => "Windows"
    };

    public static string GetDisplayName(ShellPlatform platform) => GetPagePlatformName(platform);

    /// <summary>从包名 / 目录名的平台后缀反推平台。</summary>
    public static ShellPlatform FromSuffix(string suffix)
    {
        if (suffix.StartsWith("win", StringComparison.OrdinalIgnoreCase)) return ShellPlatform.Windows;
        if (suffix.StartsWith("linux", StringComparison.OrdinalIgnoreCase)) return ShellPlatform.Linux;
        if (suffix.StartsWith("macos", StringComparison.OrdinalIgnoreCase)) return ShellPlatform.Mac;
        return Current;
    }

    public static string GetInstalledId(GodotRelease release, bool mono, ShellPlatform platform) =>
        $"{release.Id}{(mono ? "-mono" : string.Empty)}-{platform.ToString().ToLowerInvariant()}";

    public static string GetArchiveName(GodotRelease release, bool mono, ShellPlatform platform) => platform switch
    {
        ShellPlatform.Mac => $"Godot_v{release.Id}_{Prefix(mono)}macos.universal.zip",
        ShellPlatform.Linux => $"Godot_v{release.Id}_{Prefix(mono)}linux.x86_64.zip",
        _ => mono
            ? $"Godot_v{release.Id}_mono_win64.zip"
            : $"Godot_v{release.Id}_win64.exe.zip"
    };

    public static string GetInstallDirectoryName(GodotRelease release, bool mono, ShellPlatform platform) =>
        platform switch
        {
            ShellPlatform.Mac => $"Godot_v{release.Id}_{Prefix(mono)}macos.universal",
            ShellPlatform.Linux => $"Godot_v{release.Id}_{Prefix(mono)}linux.x86_64",
            _ => $"Godot_v{release.Id}_{Prefix(mono)}win64"
        };

    private static string Prefix(bool mono) => mono ? "mono_" : string.Empty;
}
