using System;
using System.Diagnostics;
using System.IO;

namespace GodotManager.Services;

public enum ShellPlatform
{
    Windows,
    Mac,
    Linux
}

/// <summary>
/// 跨平台打开链接与目录：Windows 资源管理器、macOS Finder、Linux 文件管理器。
/// CreateOpenCommand 把平台分支抽出来，便于验证各平台生成的命令。
/// </summary>
public static class ShellService
{
    public static ShellPlatform Current => OperatingSystem.IsWindows()
        ? ShellPlatform.Windows
        : OperatingSystem.IsMacOS() ? ShellPlatform.Mac : ShellPlatform.Linux;

    public static ProcessStartInfo CreateOpenCommand(string target, ShellPlatform platform, bool directory)
    {
        if (platform == ShellPlatform.Windows)
        {
            return directory
                ? WithArgument("explorer.exe", target)
                : new ProcessStartInfo(target) { UseShellExecute = true };
        }

        return WithArgument(platform == ShellPlatform.Mac ? "open" : "xdg-open", target);
    }

    public static void OpenUrl(string url)
    {
        using var process = Process.Start(CreateOpenCommand(url, Current, false));
    }

    public static void OpenDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }

        using var process = Process.Start(CreateOpenCommand(path, Current, true));
    }

    private static ProcessStartInfo WithArgument(string fileName, string argument)
    {
        var info = new ProcessStartInfo(fileName) { UseShellExecute = false };
        info.ArgumentList.Add(argument);
        return info;
    }
}
