using System;
using System.Text.Json.Serialization;

namespace GodotManager.Models;

public sealed class InstalledVersion
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Flavor { get; set; } = string.Empty;
    public bool IsMono { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string InstallDirectory { get; set; } = string.Empty;
    public DateTime InstalledAt { get; set; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrEmpty(Version)
        ? Id
        : string.IsNullOrEmpty(Flavor) ? Version : $"{Version}-{Flavor}";

    [JsonIgnore]
    public string TypeText => GodotManager.Services.LocalizationService.Instance.T(IsMono ? "TypeMono" : "TypeStandard");

    [JsonIgnore]
    public string InstalledAtText => InstalledAt.ToString("yyyy-MM-dd HH:mm");
}
