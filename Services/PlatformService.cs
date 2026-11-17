using GodotManager.Models;

namespace GodotManager.Services;

/// <summary>
/// 按当前运行平台提供下载参数、包名和安装目录名，供下载 / 解压 / 扫描共用。
/// Windows 与 Linux 取 x86_64 包，macOS 取 universal 包。
/// </summary>
public static class PlatformService
{
    public static ShellPlatform Current => ShellService.Current;

    public static (string Slug, string Platform) GetDownloadSpec(bool mono) => Current switch
    {
        ShellPlatform.Mac => mono
            ? ("mono_macos.universal.zip", "macos.universal")
            : ("macos.universal.zip", "macos.universal"),
        ShellPlatform.Linux => mono
            ? ("mono_linux_x86_64.zip", "linux.64")
            : ("linux.x86_64.zip", "linux.64"),
        _ => mono ? ("mono_win64.zip", "windows.64") : ("win64.exe.zip", "windows.64")
    };

    public static string GetArchiveName(GodotRelease release, bool mono) => Current switch
    {
        ShellPlatform.Mac => $"Godot_v{release.Id}_{Prefix(mono)}macos.universal.zip",
        ShellPlatform.Linux => $"Godot_v{release.Id}_{Prefix(mono)}linux.x86_64.zip",
        _ => mono
            ? $"Godot_v{release.Id}_mono_win64.zip"
            : $"Godot_v{release.Id}_win64.exe.zip"
    };

    public static string GetInstallDirectoryName(GodotRelease release, bool mono) => Current switch
    {
        ShellPlatform.Mac => $"Godot_v{release.Id}_{Prefix(mono)}macos.universal",
        ShellPlatform.Linux => $"Godot_v{release.Id}_{Prefix(mono)}linux.x86_64",
        _ => $"Godot_v{release.Id}_{Prefix(mono)}win64"
    };

    private static string Prefix(bool mono) => mono ? "mono_" : string.Empty;
}
