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

    private static readonly Regex ExecutableRegex = new(
        @"^Godot_v(?<version>\d+(?:\.\d+)*)-(?<flavor>[a-z]+\d*)(?<mono>_mono)?_win(?:32|64)\.exe$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string GetDownloadsDirectory(string installRoot) =>
        Path.Combine(installRoot, DownloadsDirectoryName);

    public static string GetTempZipPath(string installRoot, GodotRelease release, bool mono)
    {
        var name = mono
            ? $"Godot_v{release.Id}_mono_win64.zip"
            : $"Godot_v{release.Id}_win64.exe.zip";
        return Path.Combine(GetDownloadsDirectory(installRoot), Sanitize(name));
    }

    public static InstalledVersion Install(string zipPath, GodotRelease release, bool mono, string installRoot)
    {
        var directory = Path.Combine(installRoot, Sanitize(release.Id) + (mono ? "-mono" : ""));
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        Directory.CreateDirectory(directory);
        ZipFile.ExtractToDirectory(zipPath, directory, overwriteFiles: true);

        var executable = FindExecutable(directory, mono)
            ?? throw new InvalidOperationException("压缩包中未找到 Godot 可执行文件。");

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
    /// 识别规则：Godot_v{版本号}-{类型}[_mono]_win64.exe，排除 _console 版本。
    /// </summary>
    public static IReadOnlyList<InstalledVersion> Scan(string installRoot)
    {
        var result = new List<InstalledVersion>();
        if (!Directory.Exists(installRoot))
        {
            return result;
        }

        List<string> directories;
        List<string> rootExecutables;
        try
        {
            directories = Directory.EnumerateDirectories(installRoot).ToList();
            rootExecutables = Directory.EnumerateFiles(installRoot, "*.exe", SearchOption.TopDirectoryOnly).ToList();
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

            var executable = FindGodotExecutable(directory);
            var record = executable is null ? null : CreateScannedRecord(executable, directory);
            if (record is not null)
            {
                result.Add(record);
            }
        }

        foreach (var executable in rootExecutables)
        {
            var record = CreateScannedRecord(executable, installRoot);
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

    private static string? FindGodotExecutable(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.exe", SearchOption.AllDirectories)
                .Where(path => !Path.GetFileName(path).Contains("console", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(path => ExecutableRegex.IsMatch(Path.GetFileName(path)));
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

    private static InstalledVersion? CreateScannedRecord(string executable, string directory)
    {
        var match = ExecutableRegex.Match(Path.GetFileName(executable));
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

    private static string? FindExecutable(string directory, bool mono)
    {
        var files = Directory.EnumerateFiles(directory, "*.exe", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).Contains("console", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return files
                .Where(path => Path.GetFileName(path).Contains("mono", StringComparison.OrdinalIgnoreCase) == mono)
                .OrderBy(path => path.Count(character => character == Path.DirectorySeparatorChar))
                .ThenBy(path => path.Length)
                .FirstOrDefault()
            ?? files.OrderBy(path => path.Length).FirstOrDefault();
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
