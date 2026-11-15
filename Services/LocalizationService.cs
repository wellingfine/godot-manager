using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GodotManager.Services;

/// <summary>
/// 轻量本地化服务：界面文本通过属性绑定，运行时切换语言后通知所有属性刷新。
/// 带参数的文案用 F(key, args) 按当前语言模板格式化。
/// </summary>
public sealed class LocalizationService : ObservableObject
{
    public static LocalizationService Instance { get; } = new();

    private static readonly string[] PropertyNames = typeof(LocalizationService)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
        .Select(property => property.Name)
        .ToArray();

    private string _language = "zh";

    public string Language => _language;

    public event EventHandler? LanguageChanged;

    public string T(string key) => (_language == "en" ? En : Zh).TryGetValue(key, out var text) ? text : key;

    public string F(string key, params object[] args) => string.Format(T(key), args);

    public void SetLanguage(string language)
    {
        _language = language == "en" ? "en" : "zh";
        foreach (var name in PropertyNames)
        {
            OnPropertyChanged(name);
        }

        OnPropertyChanged(nameof(Language));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string AppTitle => T(nameof(AppTitle));
    public string AvailableLabel => T(nameof(AvailableLabel));
    public string DownloadExtract => T(nameof(DownloadExtract));
    public string NoSelection => T(nameof(NoSelection));
    public string ThemeTooltip => T(nameof(ThemeTooltip));
    public string SubtitlePrefix => T(nameof(SubtitlePrefix));
    public string SubtitleSuffix => T(nameof(SubtitleSuffix));
    public string OpenFolderTooltip => T(nameof(OpenFolderTooltip));
    public string SettingsTooltip => T(nameof(SettingsTooltip));
    public string SearchPlaceholder => T(nameof(SearchPlaceholder));
    public string RefreshTooltip => T(nameof(RefreshTooltip));
    public string ChannelLabel => T(nameof(ChannelLabel));
    public string ChannelTooltip => T(nameof(ChannelTooltip));
    public string DotNetLabel => T(nameof(DotNetLabel));
    public string Download => T(nameof(Download));
    public string News => T(nameof(News));
    public string NewsTooltip => T(nameof(NewsTooltip));
    public string CancelDownload => T(nameof(CancelDownload));
    public string InstalledLabel => T(nameof(InstalledLabel));
    public string Launch => T(nameof(Launch));
    public string OpenFolder => T(nameof(OpenFolder));
    public string Uninstall => T(nameof(Uninstall));
    public string NoInstalled => T(nameof(NoInstalled));
    public string StatusReady => T(nameof(StatusReady));
    public string TabAll => T(nameof(TabAll));
    public string SettingsTitle => T(nameof(SettingsTitle));
    public string LanguageLabel => T(nameof(LanguageLabel));
    public string LanguageHint => T(nameof(LanguageHint));
    public string InstallFolderLabel => T(nameof(InstallFolderLabel));
    public string InstallFolderHint => T(nameof(InstallFolderHint));
    public string Browse => T(nameof(Browse));
    public string BrowseLabel => T(nameof(BrowseLabel));
    public string VersionLabel => T(nameof(VersionLabel));
    public string SettingsAutosave => T(nameof(SettingsAutosave));
    public string DeletePolicyLabel => T(nameof(DeletePolicyLabel));
    public string DeletePolicyHint => T(nameof(DeletePolicyHint));
    public string DeletePolicyKeep => T(nameof(DeletePolicyKeep));
    public string DeletePolicyImmediate => T(nameof(DeletePolicyImmediate));
    public string Done => T(nameof(Done));
    public string Ok => T(nameof(Ok));
    public string Cancel => T(nameof(Cancel));
    public string ConfirmTitle => T(nameof(ConfirmTitle));
    public string AboutTitle => T(nameof(AboutTitle));
    public string AboutLabel => T(nameof(AboutLabel));
    public string AboutAuthor => T(nameof(AboutAuthor));
    public string AboutProject => T(nameof(AboutProject));
    public string AboutDescription => T(nameof(AboutDescription));

    private static readonly Dictionary<string, string> Zh = new()
    {
        ["AppTitle"] = "Godot 版本管理器",
        ["SubtitlePrefix"] = "数据来自 ",
        ["SubtitleSuffix"] = "，下载解压后即可直接启动",
        ["OpenFolderTooltip"] = "点击在文件浏览器中打开",
        ["StatusOpenUrlFailed"] = "打开链接失败：{0}",
        ["SettingsTooltip"] = "设置",
        ["SearchPlaceholder"] = "搜索版本号，例如 4.5",
        ["RefreshTooltip"] = "刷新版本列表（30 秒内重复点击使用本地缓存）",
        ["ChannelLabel"] = "版本类型",
        ["ChannelTooltip"] = "勾选要显示的版本类型，默认勾 Stable / RC / Beta",
        ["DotNetLabel"] = ".NET 版",
        ["Download"] = "下载",
        ["News"] = "新闻",
        ["NewsTooltip"] = "查看该版本的发布公告",
        ["CancelDownload"] = "取消下载",
        ["InstalledLabel"] = "已安装",
        ["Launch"] = "启动",
        ["OpenFolder"] = "打开目录",
        ["Uninstall"] = "卸载",
        ["NoInstalled"] = "还没有安装任何版本，请从左侧下载。",
        ["StatusReady"] = "正在从官网获取版本列表…",
        ["TabAll"] = "全部",
        ["TypeMono"] = ".NET (Mono)",
        ["TypeStandard"] = "标准版",
        ["SettingsTitle"] = "设置",
        ["LanguageLabel"] = "界面语言",
        ["LanguageHint"] = "切换后立即生效，下次打开会记住你的选择。",
        ["InstallFolderLabel"] = "Godot 安装位置",
        ["InstallFolderHint"] = "已安装版本与新下载的版本都放在这里；启动时会扫描该目录。",
        ["Browse"] = "更改...",
        ["DeletePolicyLabel"] = "安装包处理",
        ["DeletePolicyHint"] = "两种方式都不会动安装目录里已有的文件。",
        ["DeletePolicyKeep"] = "保留 30 天，启动时自动清理过期文件",
        ["DeletePolicyImmediate"] = "安装完成后立即删除",
        ["Done"] = "完成",
        ["Ok"] = "确定",
        ["Cancel"] = "取消",
        ["ConfirmTitle"] = "卸载 Godot 版本",
        ["AboutTitle"] = "关于",
        ["AboutLabel"] = "关于本软件",
        ["AboutAuthor"] = "作者",
        ["AboutProject"] = "项目地址",
        ["AboutDescription"] = "使用 Avalonia 与 .NET 10 构建的 Godot 版本管理器，用于下载、管理并启动各个 Godot 版本。",
        ["StatusLoading"] = "正在从 godotengine.org 获取版本列表…",
        ["StatusCached"] = "已从本地缓存加载 {0} 个版本（缓存于 {1:yyyy-MM-dd HH:mm}）。",
        ["StatusFetched"] = "已联网获取 {0} 个版本（按版本号从大到小，稳定版 + 已勾选的预发布类型）。",
        ["StatusCleaned"] = " 已清理 {0} 个超过 {1} 天的安装包。",
        ["StatusCooldown"] = "距上次刷新不足 {0} 秒，已从本地缓存解析 {1} 个版本。",
        ["StatusOfflineFallback"] = "联网失败（{0}），已使用本地缓存 {1} 个版本（缓存于 {2:yyyy-MM-dd HH:mm}）。",
        ["StatusFetchFailed"] = "获取版本列表失败：{0}",
        ["StatusDownloading"] = "正在下载 {0}…",
        ["StatusExtracting"] = "下载完成，正在解压…",
        ["StatusInstalled"] = "{0} 已安装，点击「启动」即可打开。",
        ["StatusArchiveKept"] = " 安装包已保留在 downloads 目录。",
        ["StatusCancelled"] = "下载已取消。",
        ["StatusDownloadFailed"] = "下载失败：{0}",
        ["StatusAlreadyInstalled"] = "{0} 已安装，可直接启动。",
        ["StatusSelectInstalled"] = "请先选择已安装的版本。",
        ["StatusExeMissing"] = "{0} 的可执行文件不存在，可能已被移动或删除。",
        ["StatusLaunched"] = "已启动 {0}。",
        ["StatusLaunchFailed"] = "启动失败：{0}",
        ["StatusFolderMissing"] = "安装目录不存在。",
        ["StatusOpenFolderFailed"] = "打开目录失败：{0}",
        ["StatusSelectUninstall"] = "请先选择要卸载的版本。",
        ["StatusUninstalled"] = "已卸载 {0}。",
        ["StatusUninstallFailed"] = "卸载失败：{0}",
        ["ConfirmDeleteDirectory"] = "将删除目录：\n{0}\n\n确定继续吗？",
        ["ConfirmDeleteExe"] = "可执行文件直接放在安装目录中，将只删除：\n{0}\n\n确定继续吗？",
        ["StatusDirChanged"] = "安装目录已设为 {0}，发现 {1} 个版本",
        ["StatusDirCleaned"] = "，清理了 {0} 个过期安装包",
        ["StatusPeriod"] = "。",
        ["StatusDirFailed"] = "设置安装目录失败：{0}",
        ["StatusSettingsSaved"] = "设置已保存。",
        ["FilteredCount"] = "{0} 个版本",
        ["AvailableLabel"] = "可用版本",
        ["DownloadExtract"] = "下载并解压",
        ["NoSelection"] = "请选择一个版本",
        ["ThemeTooltip"] = "切换深色 / 浅色主题",
        ["SelectedItem"] = "已选 {0} · {1}",
        ["InstalledCount"] = "{0} 个版本",
        ["ChannelNone"] = "未选择",
        ["BrowseLabel"] = "更改目录",
        ["VersionLabel"] = "版本",
        ["SettingsAutosave"] = "设置立即生效，无需保存"
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["AppTitle"] = "Godot Version Manager",
        ["SubtitlePrefix"] = "Releases from ",
        ["SubtitleSuffix"] = " — download, extract and launch",
        ["OpenFolderTooltip"] = "Click to open in your file manager",
        ["StatusOpenUrlFailed"] = "Failed to open link: {0}",
        ["SettingsTooltip"] = "Settings",
        ["SearchPlaceholder"] = "Search versions, e.g. 4.5",
        ["RefreshTooltip"] = "Refresh releases (uses local cache within 30 seconds)",
        ["ChannelLabel"] = "Channels",
        ["ChannelTooltip"] = "Select which release channels to show; Stable, RC and Beta are checked by default.",
        ["DotNetLabel"] = ".NET build",
        ["Download"] = "Download",
        ["News"] = "News",
        ["NewsTooltip"] = "Read the release announcement",
        ["CancelDownload"] = "Cancel download",
        ["InstalledLabel"] = "Installed",
        ["Launch"] = "Launch",
        ["OpenFolder"] = "Open folder",
        ["Uninstall"] = "Uninstall",
        ["NoInstalled"] = "No versions installed yet. Download one from the left.",
        ["StatusReady"] = "Loading releases from godotengine.org…",
        ["TabAll"] = "All",
        ["TypeMono"] = ".NET (Mono)",
        ["TypeStandard"] = "Standard",
        ["SettingsTitle"] = "Settings",
        ["LanguageLabel"] = "Display language",
        ["LanguageHint"] = "Applies immediately and is remembered next time.",
        ["InstallFolderLabel"] = "Installation folder",
        ["InstallFolderHint"] = "Installed and newly downloaded versions live here. This folder is scanned on startup.",
        ["Browse"] = "Browse...",
        ["DeletePolicyLabel"] = "Archive handling",
        ["DeletePolicyHint"] = "Existing files in the installation folder are never touched.",
        ["DeletePolicyKeep"] = "Keep for 30 days, clean up expired files on startup",
        ["DeletePolicyImmediate"] = "Delete right after installation",
        ["Done"] = "Done",
        ["Ok"] = "OK",
        ["Cancel"] = "Cancel",
        ["ConfirmTitle"] = "Uninstall Godot version",
        ["AboutTitle"] = "About",
        ["AboutLabel"] = "About this app",
        ["AboutAuthor"] = "Author",
        ["AboutProject"] = "Project",
        ["AboutDescription"] = "A Godot version manager built with Avalonia and .NET 10 to download, manage and launch Godot releases.",
        ["StatusLoading"] = "Fetching releases from godotengine.org…",
        ["StatusCached"] = "Loaded {0} releases from local cache (cached at {1:yyyy-MM-dd HH:mm}).",
        ["StatusFetched"] = "Fetched {0} releases online (sorted by version, stable + selected pre-release channels).",
        ["StatusCleaned"] = " Cleaned up {0} archives older than {1} days.",
        ["StatusCooldown"] = "Less than {0} seconds since last refresh; parsed {1} releases from local cache.",
        ["StatusOfflineFallback"] = "Network failed ({0}); using local cache with {1} releases (cached at {2:yyyy-MM-dd HH:mm}).",
        ["StatusFetchFailed"] = "Failed to fetch releases: {0}",
        ["StatusDownloading"] = "Downloading {0}…",
        ["StatusExtracting"] = "Download complete, extracting…",
        ["StatusInstalled"] = "{0} installed. Click Launch to open it.",
        ["StatusArchiveKept"] = " Archive kept in the downloads folder.",
        ["StatusCancelled"] = "Download cancelled.",
        ["StatusDownloadFailed"] = "Download failed: {0}",
        ["StatusAlreadyInstalled"] = "{0} is already installed; launch it directly.",
        ["StatusSelectInstalled"] = "Select an installed version first.",
        ["StatusExeMissing"] = "Executable for {0} is missing; it may have been moved or deleted.",
        ["StatusLaunched"] = "Launched {0}.",
        ["StatusLaunchFailed"] = "Launch failed: {0}",
        ["StatusFolderMissing"] = "Installation folder does not exist.",
        ["StatusOpenFolderFailed"] = "Failed to open folder: {0}",
        ["StatusSelectUninstall"] = "Select a version to uninstall first.",
        ["StatusUninstalled"] = "Uninstalled {0}.",
        ["StatusUninstallFailed"] = "Uninstall failed: {0}",
        ["ConfirmDeleteDirectory"] = "This will delete the folder:\n{0}\n\nContinue?",
        ["ConfirmDeleteExe"] = "The executable sits directly in the installation folder; only this file will be deleted:\n{0}\n\nContinue?",
        ["StatusDirChanged"] = "Installation folder set to {0}; found {1} versions",
        ["StatusDirCleaned"] = ", cleaned up {0} expired archives",
        ["StatusPeriod"] = ".",
        ["StatusDirFailed"] = "Failed to set installation folder: {0}",
        ["StatusSettingsSaved"] = "Settings saved.",
        ["FilteredCount"] = "{0} versions",
        ["AvailableLabel"] = "Available",
        ["DownloadExtract"] = "Download & extract",
        ["NoSelection"] = "Select a version",
        ["ThemeTooltip"] = "Toggle dark / light theme",
        ["SelectedItem"] = "Selected {0} · {1}",
        ["InstalledCount"] = "{0} versions",
        ["ChannelNone"] = "None",
        ["BrowseLabel"] = "Change folder",
        ["VersionLabel"] = "Version",
        ["SettingsAutosave"] = "Changes apply instantly — no need to save"
    };
}
