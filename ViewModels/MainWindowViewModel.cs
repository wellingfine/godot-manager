using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodotManager.Models;
using GodotManager.Services;

namespace GodotManager.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppStateService _stateService = new();
    private readonly DownloadService _downloadService = new();
    private readonly ReleaseCacheService _cacheService = new();
    private readonly Func<Task<string?>> _pickFolder;
    private readonly Func<string, string, Task<bool>> _confirm;
    private CancellationTokenSource? _downloadCts;
    private int _expiredDownloadsCleaned;
    private bool _rebuildingGroups;
    private DateTime? _lastRefreshUtc;

    public LocalizationService L => LocalizationService.Instance;

    public ObservableCollection<GodotRelease> Releases { get; } = new();
    public ObservableCollection<GodotRelease> FilteredReleases { get; } = new();
    public ObservableCollection<InstalledVersion> Installed { get; } = new();
    public ObservableCollection<MajorGroup> MajorGroups { get; } = new();

    public bool HasNoInstalled => Installed.Count == 0;
    public string FilteredCountText => L.F("FilteredCount", FilteredReleases.Count);
    public string InstalledCountText => L.F("InstalledCount", Installed.Count);

    public string ChannelSummaryText
    {
        get
        {
            var names = new List<string>();
            if (ShowStable) names.Add("Stable");
            if (ShowRc) names.Add("RC");
            if (ShowBeta) names.Add("Beta");
            if (ShowDev) names.Add("Dev");
            if (ShowAlpha) names.Add("Alpha");
            var summary = names.Count == 0 ? L.T("ChannelNone") : string.Join(" / ", names);
            return $"{L.T("ChannelLabel")} · {summary}";
        }
    }

    public string SelectedSummaryText => SelectedRelease is null
        ? L.T("NoSelection")
        : L.F("SelectedItem", SelectedRelease.Id, UseMono ? ".NET" : L.T("TypeStandard"));

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _showStable = true;
    [ObservableProperty] private bool _showRc = true;
    [ObservableProperty] private bool _showBeta = true;
    [ObservableProperty] private bool _showDev;
    [ObservableProperty] private bool _showAlpha;
    [ObservableProperty] private MajorGroup? _selectedMajor;
    [ObservableProperty] private bool _useMono = true;
    [ObservableProperty] private bool _deleteArchiveAfterInstall;
    [ObservableProperty] private int _languageIndex;
    [ObservableProperty] private int _deletePolicyIndex;
    [ObservableProperty] private string _installDirectory = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isDarkTheme;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    private GodotRelease? _selectedRelease;

    [ObservableProperty]
    private InstalledVersion? _selectedInstalled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelDownloadCommand))]
    private bool _isDownloading;

    public MainWindowViewModel(Func<Task<string?>> pickFolder, Func<string, string, Task<bool>> confirm)
    {
        _pickFolder = pickFolder;
        _confirm = confirm;

        L.SetLanguage(_stateService.State.Language);
        _isDarkTheme = _stateService.State.Theme == "dark";
        App.ApplyTheme(_isDarkTheme);
        InstallDirectory = _stateService.State.InstallDirectory;
        _deleteArchiveAfterInstall = _stateService.State.DeleteArchiveAfterInstall;
        _deletePolicyIndex = _deleteArchiveAfterInstall ? 1 : 0;
        _useMono = _stateService.State.UseMono;
        _languageIndex = _stateService.State.Language == "en" ? 1 : 0;
        _showStable = _stateService.State.ShowStable;
        _showRc = _stateService.State.ShowRc;
        _showBeta = _stateService.State.ShowBeta;
        _showDev = _stateService.State.ShowDev;
        _showAlpha = _stateService.State.ShowAlpha;
        Status = L.T("StatusReady");

        MajorGroups.Add(new MajorGroup(L.T("TabAll"), null));
        if (_stateService.State.SelectedMajor is int major)
        {
            MajorGroups.Add(new MajorGroup($"{major}.x", major));
            _selectedMajor = MajorGroups[1];
        }
        else
        {
            _selectedMajor = MajorGroups[0];
        }

        Installed.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasNoInstalled));
            OnPropertyChanged(nameof(InstalledCountText));
        };
        FilteredReleases.CollectionChanged += (_, _) => OnPropertyChanged(nameof(FilteredCountText));
        L.LanguageChanged += OnLanguageChanged;

        _expiredDownloadsCleaned = InstallService.CleanupExpiredDownloads(
            InstallDirectory, _stateService.State.TempFileRetentionDays);
        RefreshInstalled();

        _ = LoadReleasesCommand.ExecuteAsync(null);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RebuildMajorGroups();
        OnPropertyChanged(nameof(FilteredCountText));
        OnPropertyChanged(nameof(InstalledCountText));
        OnPropertyChanged(nameof(ChannelSummaryText));
        OnPropertyChanged(nameof(SelectedSummaryText));
        RefreshInstalled();
        Status = L.T("StatusSettingsSaved");
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedReleaseChanged(GodotRelease? value) =>
        OnPropertyChanged(nameof(SelectedSummaryText));

    partial void OnUseMonoChanged(bool value)
    {
        _stateService.State.UseMono = value;
        _stateService.Save();

        foreach (var release in Releases)
        {
            release.ShowDotNetTag = value;
        }

        OnPropertyChanged(nameof(SelectedSummaryText));
        ApplyFilter();
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        _stateService.State.Theme = value ? "dark" : "light";
        _stateService.Save();
        App.ApplyTheme(value);
    }

    [RelayCommand]
    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;

    partial void OnLanguageIndexChanged(int value)
    {
        var language = value == 1 ? "en" : "zh";
        _stateService.State.Language = language;
        _stateService.Save();
        L.SetLanguage(language);
    }

    partial void OnDeletePolicyIndexChanged(int value) => DeleteArchiveAfterInstall = value == 1;

    partial void OnDeleteArchiveAfterInstallChanged(bool value)
    {
        _stateService.State.DeleteArchiveAfterInstall = value;
        _stateService.Save();
    }

    partial void OnShowStableChanged(bool value) => OnChannelFilterChanged();
    partial void OnShowRcChanged(bool value) => OnChannelFilterChanged();
    partial void OnShowBetaChanged(bool value) => OnChannelFilterChanged();
    partial void OnShowDevChanged(bool value) => OnChannelFilterChanged();
    partial void OnShowAlphaChanged(bool value) => OnChannelFilterChanged();

    private void OnChannelFilterChanged()
    {
        _stateService.State.ShowStable = ShowStable;
        _stateService.State.ShowRc = ShowRc;
        _stateService.State.ShowBeta = ShowBeta;
        _stateService.State.ShowDev = ShowDev;
        _stateService.State.ShowAlpha = ShowAlpha;
        _stateService.Save();
        OnPropertyChanged(nameof(ChannelSummaryText));
        ApplyFilter();
    }

    partial void OnSelectedMajorChanged(MajorGroup? value)
    {
        if (_rebuildingGroups)
        {
            return;
        }

        _stateService.State.SelectedMajor = value?.Major;
        _stateService.Save();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredReleases.Clear();
        var keyword = SearchText.Trim();
        var major = SelectedMajor?.Major;

        foreach (var release in Releases.Where(release =>
                     IsChannelEnabled(release.Channel) &&
                     (major is null || GodotVersionComparer.GetMajor(release.Version) == major) &&
                     (keyword.Length == 0 || release.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase))))
        {
            FilteredReleases.Add(release);
        }

        // 保持列表始终有一个选中项，底部「已选」与下载按钮在打开后即可用。
        if (FilteredReleases.Count == 0)
        {
            SelectedRelease = null;
        }
        else if (SelectedRelease is null || !FilteredReleases.Contains(SelectedRelease))
        {
            SelectedRelease = FilteredReleases[0];
        }
    }

    private bool IsChannelEnabled(string channel) => channel switch
    {
        "stable" => ShowStable,
        "rc" => ShowRc,
        "beta" => ShowBeta,
        "dev" => ShowDev,
        "alpha" => ShowAlpha,
        _ => false
    };

    private void RebuildMajorGroups()
    {
        var previous = SelectedMajor?.Major;
        _rebuildingGroups = true;
        try
        {
            MajorGroups.Clear();
            MajorGroups.Add(new MajorGroup(L.T("TabAll"), null));

            foreach (var major in Releases
                         .Select(release => GodotVersionComparer.GetMajor(release.Version))
                         .Where(major => major > 0)
                         .Distinct()
                         .OrderByDescending(major => major))
            {
                MajorGroups.Add(new MajorGroup($"{major}.x", major));
            }

            SelectedMajor = MajorGroups.FirstOrDefault(group => group.Major == previous) ?? MajorGroups[0];
        }
        finally
        {
            _rebuildingGroups = false;
        }

        ApplyFilter();
    }

    /// <summary>
    /// 扫描当前安装目录，把已存在的版本补进记录，并只显示属于该目录的版本。
    /// </summary>
    private void RefreshInstalled()
    {
        var root = Path.GetFullPath(InstallDirectory);
        var records = _stateService.State.Installed;

        foreach (var found in InstallService.Scan(root))
        {
            if (!records.Any(record => PathsEqual(record.ExecutablePath, found.ExecutablePath)))
            {
                records.Add(found);
            }
        }

        records.RemoveAll(record =>
            IsUnderRoot(record.ExecutablePath, root) && !File.Exists(record.ExecutablePath));

        Installed.Clear();
        foreach (var record in records
                     .Where(record => IsUnderRoot(record.ExecutablePath, root))
                     .OrderByDescending(record => record.Version, GodotVersionComparer.Instance)
                     .ThenByDescending(record => record.InstalledAt))
        {
            Installed.Add(record);
        }

        // 已安装列表同样保持默认选中，启动按钮打开界面后即可对应最新版本。
        if (Installed.Count == 0)
        {
            SelectedInstalled = null;
        }
        else if (SelectedInstalled is null || !Installed.Contains(SelectedInstalled))
        {
            SelectedInstalled = Installed[0];
        }

        _stateService.Save();
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);

    private static bool IsUnderRoot(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private async Task LoadReleasesAsync()
    {
        if (IsLoading)
        {
            return;
        }

        // 旧版本生成的缓存没有公告链接，直接联网重抓一次把它升级；联网失败时仍会回退到旧缓存。
        if (TryLoadFromCache(out var cached, out var cachedAt) &&
            cached.Any(release => !string.IsNullOrEmpty(release.NewsUrl)))
        {
            SetReleases(cached);
            Status = L.F("StatusCached", cached.Count, cachedAt) + AppendCleanupStatus();
            return;
        }

        await FetchFromNetworkAsync(fromRefresh: false);
    }

    [RelayCommand]
    private async Task RefreshReleasesAsync()
    {
        if (IsLoading)
        {
            return;
        }

        var cooldown = TimeSpan.FromSeconds(Math.Max(1, _stateService.State.RefreshCooldownSeconds));
        if (_lastRefreshUtc is not null && DateTime.UtcNow - _lastRefreshUtc.Value < cooldown)
        {
            if (TryLoadFromCache(out var cached, out _, ignoreExpiration: true))
            {
                SetReleases(cached);
                Status = L.F("StatusCooldown", (int)cooldown.TotalSeconds, cached.Count);
                return;
            }
        }

        await FetchFromNetworkAsync(fromRefresh: true);
    }

    private async Task FetchFromNetworkAsync(bool fromRefresh)
    {
        IsLoading = true;
        try
        {
            Status = L.T("StatusLoading");
            var releases = await GodotArchiveService.FetchAsync();
            if (fromRefresh)
            {
                _lastRefreshUtc = DateTime.UtcNow;
            }

            _cacheService.Save(releases);
            SetReleases(releases);
            Status = L.F("StatusFetched", releases.Count) + AppendCleanupStatus();
        }
        catch (Exception exception)
        {
            if (TryLoadFromCache(out var cached, out var cachedAt, ignoreExpiration: true))
            {
                SetReleases(cached);
                Status = L.F("StatusOfflineFallback", exception.Message, cached.Count, cachedAt);
            }
            else
            {
                Status = L.F("StatusFetchFailed", exception.Message);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool TryLoadFromCache(out IReadOnlyList<GodotRelease> releases, out DateTime cachedAt,
        bool ignoreExpiration = false)
    {
        releases = Array.Empty<GodotRelease>();
        cachedAt = default;

        var cached = _cacheService.Load(out var timestamp);
        if (cached is null || cached.Count == 0)
        {
            return false;
        }

        var lifetime = TimeSpan.FromDays(Math.Max(1, _stateService.State.CacheExpirationDays));
        if (!ignoreExpiration && DateTime.Now - timestamp > lifetime)
        {
            return false;
        }

        releases = cached;
        cachedAt = timestamp;
        return true;
    }

    private void SetReleases(IReadOnlyList<GodotRelease> releases)
    {
        Releases.Clear();
        foreach (var release in releases)
        {
            release.ShowDotNetTag = UseMono;
            Releases.Add(release);
        }

        RebuildMajorGroups();
        ApplyFilter();
    }

    private string AppendCleanupStatus()
    {
        if (_expiredDownloadsCleaned <= 0)
        {
            return string.Empty;
        }

        var text = L.F("StatusCleaned", _expiredDownloadsCleaned,
            _stateService.State.TempFileRetentionDays);
        _expiredDownloadsCleaned = 0;
        return text;
    }

    private bool CanDownload(GodotRelease? release) =>
        !IsDownloading && (release is not null || SelectedRelease is not null);

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync(GodotRelease? release)
    {
        release ??= SelectedRelease;
        if (release is null || IsDownloading)
        {
            return;
        }

        var mono = UseMono;
        var deleteArchive = DeleteArchiveAfterInstall;
        var id = release.Id + (mono ? "-mono" : "");
        if (Installed.Any(version => version.Id == id))
        {
            Status = L.F("StatusAlreadyInstalled", release.Id);
            return;
        }

        IsDownloading = true;
        _downloadCts = new CancellationTokenSource();
        var zipPath = InstallService.GetTempZipPath(InstallDirectory, release, mono);
        var keepArchive = false;

        try
        {
            Progress = 0;
            ProgressText = string.Empty;
            Status = L.F("StatusDownloading", release.Id + (mono ? " (.NET)" : string.Empty));

            var progress = new Progress<DownloadProgress>(value =>
            {
                Progress = value.Percent;
                ProgressText = value.Text;
            });

            await _downloadService.DownloadAsync(
                DownloadService.BuildDownloadUrl(release, mono), zipPath, progress, _downloadCts.Token);

            Status = L.T("StatusExtracting");
            var installed = await Task.Run(() => InstallService.Install(zipPath, release, mono, InstallDirectory));

            _stateService.State.Installed.RemoveAll(version =>
                PathsEqual(version.ExecutablePath, installed.ExecutablePath));
            _stateService.State.Installed.Add(installed);
            _stateService.Save();
            RefreshInstalled();
            keepArchive = !deleteArchive;

            Status = L.F("StatusInstalled", installed.DisplayName) +
                     (deleteArchive ? string.Empty : L.T("StatusArchiveKept"));
        }
        catch (OperationCanceledException)
        {
            Status = L.T("StatusCancelled");
        }
        catch (Exception exception)
        {
            Status = L.F("StatusDownloadFailed", exception.Message);
        }
        finally
        {
            if (!keepArchive)
            {
                try
                {
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }
                }
                catch (IOException)
                {
                    // 临时文件清理失败不影响安装结果。
                }
            }

            _downloadCts?.Dispose();
            _downloadCts = null;
            IsDownloading = false;
            Progress = 0;
            ProgressText = string.Empty;
        }
    }

    private bool CanCancelDownload() => IsDownloading;

    [RelayCommand(CanExecute = nameof(CanCancelDownload))]
    private void CancelDownload() => _downloadCts?.Cancel();

    [RelayCommand]
    private void Launch(InstalledVersion? version)
    {
        var target = version ?? SelectedInstalled;
        if (target is null)
        {
            Status = L.T("StatusSelectInstalled");
            return;
        }

        if (!File.Exists(target.ExecutablePath))
        {
            Status = L.F("StatusExeMissing", target.DisplayName);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(target.ExecutablePath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(target.ExecutablePath)
            });
            Status = L.F("StatusLaunched", target.DisplayName);
        }
        catch (Exception exception)
        {
            Status = L.F("StatusLaunchFailed", exception.Message);
        }
    }

    [RelayCommand]
    private void OpenNews(GodotRelease? release)
    {
        if (release is null)
        {
            return;
        }

        var url = string.IsNullOrEmpty(release.NewsUrl) ? release.DownloadPageUrl : release.NewsUrl;
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        try
        {
            ShellService.OpenUrl(url);
        }
        catch (Exception exception)
        {
            Status = L.F("StatusOpenUrlFailed", exception.Message);
        }
    }

    [RelayCommand]
    private void OpenArchiveUrl()
    {
        try
        {
            ShellService.OpenUrl(GodotArchiveService.ArchiveUrl);
        }
        catch (Exception exception)
        {
            Status = L.F("StatusOpenUrlFailed", exception.Message);
        }
    }

    [RelayCommand]
    private void OpenInstallDirectory()
    {
        if (!Directory.Exists(InstallDirectory))
        {
            Status = L.T("StatusFolderMissing");
            return;
        }

        try
        {
            ShellService.OpenDirectory(InstallDirectory);
        }
        catch (Exception exception)
        {
            Status = L.F("StatusOpenFolderFailed", exception.Message);
        }
    }

    [RelayCommand]
    private void OpenFolder(InstalledVersion? version)
    {
        var path = (version ?? SelectedInstalled)?.InstallDirectory;
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            Status = L.T("StatusFolderMissing");
            return;
        }

        try
        {
            ShellService.OpenDirectory(path);
        }
        catch (Exception exception)
        {
            Status = L.F("StatusOpenFolderFailed", exception.Message);
        }
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        var version = SelectedInstalled;
        if (version is null)
        {
            Status = L.T("StatusSelectUninstall");
            return;
        }

        var root = Path.GetFullPath(InstallDirectory).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var target = Path.GetFullPath(version.InstallDirectory).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var isInstallRoot = string.Equals(root, target, StringComparison.OrdinalIgnoreCase);

        var message = isInstallRoot
            ? L.F("ConfirmDeleteExe", version.ExecutablePath)
            : L.F("ConfirmDeleteDirectory", version.InstallDirectory);
        if (!await _confirm(L.T("ConfirmTitle"), message))
        {
            return;
        }

        try
        {
            if (isInstallRoot)
            {
                if (File.Exists(version.ExecutablePath))
                {
                    File.Delete(version.ExecutablePath);
                }
            }
            else if (Directory.Exists(version.InstallDirectory))
            {
                Directory.Delete(version.InstallDirectory, true);
            }

            _stateService.State.Installed.RemoveAll(item =>
                PathsEqual(item.ExecutablePath, version.ExecutablePath));
            _stateService.Save();
            RefreshInstalled();
            Status = L.F("StatusUninstalled", version.DisplayName);
        }
        catch (Exception exception)
        {
            Status = L.F("StatusUninstallFailed", exception.Message);
        }
    }

    [RelayCommand]
    private async Task BrowseInstallDirectoryAsync()
    {
        try
        {
            var path = await _pickFolder();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Directory.CreateDirectory(path);
            InstallDirectory = path;
            _stateService.State.InstallDirectory = path;
            _stateService.Save();

            var cleaned = InstallService.CleanupExpiredDownloads(path, _stateService.State.TempFileRetentionDays);
            RefreshInstalled();
            Status = L.F("StatusDirChanged", path, Installed.Count) +
                     (cleaned > 0 ? L.F("StatusDirCleaned", cleaned) : string.Empty) +
                     L.T("StatusPeriod");
        }
        catch (Exception exception)
        {
            Status = L.F("StatusDirFailed", exception.Message);
        }
    }
}
