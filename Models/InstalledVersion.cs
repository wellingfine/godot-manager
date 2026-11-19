using System;
using System.Text.Json.Serialization;

namespace GodotManager.Models;

public sealed class InstalledVersion
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Flavor { get; set; } = string.Empty;
    public bool IsMono { get; set; }
    /// <summary>下载目标平台（ShellPlatform 枚举名）；空值兼容旧记录，按当前系统处理。</summary>
    public string Platform { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string InstallDirectory { get; set; } = string.Empty;
    public DateTime InstalledAt { get; set; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrEmpty(Version)
        ? Id
        : string.IsNullOrEmpty(Flavor) ? Version : $"{Version}-{Flavor}";

    [JsonIgnore]
    public GodotManager.Services.ShellPlatform TargetPlatform =>
        GodotManager.Services.PlatformService.Parse(Platform);

    [JsonIgnore]
    public string PlatformText => GodotManager.Services.PlatformService.GetDisplayName(TargetPlatform);

    [JsonIgnore]
    public string TypeText => GodotManager.Services.LocalizationService.Instance.T(IsMono ? "TypeMono" : "TypeStandard");

    [JsonIgnore]
    public string InstalledAtText => InstalledAt.ToString("yyyy-MM-dd HH:mm");

    [JsonIgnore]
    public string DetailText => $"{TypeText} · {PlatformText} · {InstalledAtText}";
}
