using System;
using System.Collections.Generic;
using System.IO;

namespace GodotManager.Models;

public sealed class AppState
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GodotManager");

    public static string DefaultInstallDirectory => Path.Combine(DataDirectory, "versions");

    public string InstallDirectory { get; set; } = DefaultInstallDirectory;
    public string Language { get; set; } = "zh";
    public bool UseMono { get; set; } = true;
    public int? SelectedMajor { get; set; }
    public bool DeleteArchiveAfterInstall { get; set; }
    public bool ShowStable { get; set; } = true;
    public bool ShowRc { get; set; } = true;
    public bool ShowBeta { get; set; } = true;
    public bool ShowDev { get; set; }
    public bool ShowAlpha { get; set; }
    public int TempFileRetentionDays { get; set; } = 30;
    public int CacheExpirationDays { get; set; } = 2;
    public int RefreshCooldownSeconds { get; set; } = 30;
    public List<InstalledVersion> Installed { get; set; } = new();
}
