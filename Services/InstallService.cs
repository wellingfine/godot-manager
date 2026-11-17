using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using GodotManager.Models;

namespace GodotManager.Services;

public static class InstallService
{
    public const string DownloadsDirectoryName = "downloads";

    // 官方各平台包名：Windows 的 exe 带 .exe，Linux 是无扩展名二进制，macOS 用版本目录名识别。
    private static readonly Regex GodotNameRegex = new(
        @"^Godot_v(?<version>\d+(?:\.\d+)*)-(?<flavor>[a-z]+\d*)(?<mono>_mono)?_(?:win(?:32|64)\.exe|linux\.[a-z0-9_]+|macos\.universal)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string GetDownloadsDirectory(string installRoot) =>
        Path.Combine(installRoot, DownloadsDirectoryName);

    public static string GetTempZipPath(string installRoot, GodotRelease release, bool mono)
    {
        var name = PlatformService.GetArchiveName(release, mono);
        return Path.Combine(GetDownloadsDirectory(installRoot), Sanitize(name));
    }

    public static InstalledVersion Install(string zipPath, GodotRelease release, bool mono, string installRoot)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var platform = PlatformService.Current;
        var main = FindMainExecutable(archive.Entries, mono, platform)
            ?? throw new InvalidOperationException("压缩包中未找到 Godot 可执行文件。");

        // 目录名沿用官方包命名（如 Godot_v4.4.1-stable_mono_win64），不再自己拼版本号。
        var directory = Path.Combine(installRoot, Sanitize(PlatformService.GetInstallDirectoryName(release, mono)));
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        Directory.CreateDirectory(directory);
        ExtractFlattened(archive, directory, main, platform);

        var executable = FindExecutable(directory, mono, platform)
            ?? throw new InvalidOperationException("压缩包中未找到 Godot 可执行文件。");
        EnsureExecutable(executable);

        return new InstalledVersion
        {
            Id = release.Id + (mono ? "-mono" : ""),
            Version = release.Version,
            Flavor = release.Flavor,
            IsMono = mono,
            ExecutablePath = executable,
            InstallDirectory = directory,
            InstalledAt = DateTime.Now
        };
    }

    /// <summary>
    /// 扫描安装目录中已存在的 Godot（包括手动下载解压的版本）。
    /// 识别规则：Godot_v{版本号}-{类型}[_mono]_{win64.exe|linux.x86_64|macos.universal}，排除 _console 版本。
    /// </summary>
    public static IReadOnlyList<InstalledVersion> Scan(string installRoot)
    {
        var result = new List<InstalledVersion>();
        if (!Directory.Exists(installRoot))
        {
            return result;
        }

        var platform = PlatformService.Current;
        List<string> directories;
        List<string> rootExecutables;
        try
        {
            directories = Directory.EnumerateDirectories(installRoot).ToList();
            rootExecutables = platform switch
            {
                ShellPlatform.Windows => Directory.EnumerateFiles(installRoot, "*.exe", SearchOption.TopDirectoryOnly).ToList(),
                ShellPlatform.Linux => Directory.EnumerateFiles(installRoot, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => GodotNameRegex.IsMatch(Path.GetFileName(path))).ToList(),
                _ => new List<string>()
            };
        }
        catch (IOException)
        {
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            return result;
        }

        foreach (var directory in directories)
        {
            if (string.Equals(Path.GetFileName(directory), DownloadsDirectoryName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var executable = FindGodotExecutable(directory, platform);
            // macOS 的可执行文件固定叫 Godot，版本信息只能从版本目录名解析。
            var name = platform == ShellPlatform.Mac
                ? Path.GetFileName(directory)
                : Path.GetFileName(executable ?? string.Empty);
            var record = executable is null ? null : CreateScannedRecord(name, executable, directory);
            if (record is not null)
            {
                result.Add(record);
            }
        }

        foreach (var executable in rootExecutables)
        {
            var record = CreateScannedRecord(Path.GetFileName(executable), executable, installRoot);
            if (record is not null)
            {
                result.Add(record);
            }
        }

        return result;
    }

    /// <summary>
    /// 清理 downloads 目录中超过保留天数的安装包，返回删除数量。
    /// </summary>
    public static int CleanupExpiredDownloads(string installRoot, int retentionDays)
    {
        var directory = GetDownloadsDirectory(installRoot);
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var threshold = DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays));
        var removed = 0;

        List<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).ToList();
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".part", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (File.GetLastWriteTimeUtc(file) >= threshold)
                {
                    continue;
                }

                File.Delete(file);
                removed++;
            }
            catch (IOException)
            {
                // 文件被占用时跳过，下次启动再清理。
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return removed;
    }

    private static string? FindGodotExecutable(string directory, ShellPlatform platform)
    {
        try
        {
            if (platform == ShellPlatform.Mac)
            {
                return Directory.EnumerateFiles(directory, "Godot", SearchOption.AllDirectories)
                    .FirstOrDefault(path => path.Replace('\\', '/')
                        .EndsWith("/Contents/MacOS/Godot", StringComparison.OrdinalIgnoreCase));
            }

            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => !Path.GetFileName(path).Contains("console", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(path => GodotNameRegex.IsMatch(Path.GetFileName(path)));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static InstalledVersion? CreateScannedRecord(string name, string executable, string directory)
    {
        var match = GodotNameRegex.Match(name);
        if (!match.Success)
        {
            return null;
        }

        var version = match.Groups["version"].Value;
        var flavor = match.Groups["flavor"].Value.ToLowerInvariant();
        var mono = match.Groups["mono"].Success;
        var id = $"{version}-{flavor}";

        return new InstalledVersion
        {
            Id = id + (mono ? "-mono" : ""),
            Version = version,
            Flavor = flavor,
            IsMono = mono,
            ExecutablePath = executable,
            InstallDirectory = directory,
            InstalledAt = File.GetLastWriteTime(executable)
        };
    }

    private static string? FindExecutable(string directory, bool mono, ShellPlatform platform)
    {
        if (platform == ShellPlatform.Mac)
        {
            return Directory.EnumerateFiles(directory, "Godot", SearchOption.AllDirectories)
                .FirstOrDefault(path => path.Replace('\\', '/')
                    .EndsWith("/Contents/MacOS/Godot", StringComparison.OrdinalIgnoreCase));
        }

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => GodotNameRegex.IsMatch(Path.GetFileName(path)) &&
                           !Path.GetFileName(path).Contains("console", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return files
                .Where(path => Path.GetFileName(path).Contains("mono", StringComparison.OrdinalIgnoreCase) == mono)
                .OrderBy(path => path.Count(character => character == Path.DirectorySeparatorChar))
                .ThenBy(path => path.Length)
                .FirstOrDefault()
            ?? files.OrderBy(path => path.Length).FirstOrDefault();
    }

    /// <summary>
    /// 官方包结构不统一：.NET 包通常带一层同名目录（如 Godot_v4.4.1-stable_mono_win64/），标准版则直接放 exe。
    /// 以主 exe 所在目录为基准剥掉这层包装，让 exe 与 GodotSharp 直接落在官方命名的版本目录下；
    /// 包内其它根级条目（README 等）仍按原路径保留。
    /// </summary>
    private static void ExtractFlattened(ZipArchive archive, string destination, ZipArchiveEntry main,
        ShellPlatform platform)
    {
        var root = Path.GetFullPath(destination).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var name = main.FullName.Replace('\\', '/');
        var slash = name.LastIndexOf('/');
        // macOS 必须保留 Godot.app/Contents/... 的 bundle 结构，不能剥掉外层目录。
        var prefix = platform == ShellPlatform.Mac || slash < 0 || name.StartsWith('/') ||
                     name.Split('/').Any(part => part is "." or "..")
            ? null
            : name[..(slash + 1)];

        foreach (var entry in archive.Entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            if (prefix is not null && relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                relative = relative[prefix.Length..];
            }

            // 目录项按文件自动创建；异常路径（绝对路径、..、盘符）一律跳过，避免写出目标目录。
            if (entry.Name.Length == 0 || relative.Length == 0 || relative.EndsWith('/') ||
                relative.Split('/').Any(part => part.Length == 0 || part is "." or ".." || part.Contains(':')))
            {
                continue;
            }

            var target = Path.GetFullPath(Path.Combine(destination,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    /// <summary>
    /// 在压缩包中定位主 exe（忽略 _console.exe），用于确定官方目录名与需要剥掉的包装目录。
    /// </summary>
    private static ZipArchiveEntry? FindMainExecutable(IReadOnlyList<ZipArchiveEntry> entries, bool mono,
        ShellPlatform platform)
    {
        if (platform == ShellPlatform.Mac)
        {
            var apps = entries
                .Where(entry => entry.FullName.EndsWith("/Contents/MacOS/Godot", StringComparison.OrdinalIgnoreCase))
                .ToList();
            return apps
                       .FirstOrDefault(entry => entry.FullName.Contains("mono", StringComparison.OrdinalIgnoreCase) == mono)
                   ?? apps.FirstOrDefault();
        }

        var executables = entries
            .Where(entry => GodotNameRegex.IsMatch(entry.Name) &&
                            !entry.Name.Contains("console", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return executables
            .FirstOrDefault(entry => entry.Name.Contains("mono", StringComparison.OrdinalIgnoreCase) == mono)
            ?? executables.FirstOrDefault();
    }

    private static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute |
                                            UnixFileMode.OtherExecute);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string Sanitize(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }
}
