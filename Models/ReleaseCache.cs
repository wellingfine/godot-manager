using System;
using System.Collections.Generic;

namespace GodotManager.Models;

public sealed class ReleaseCache
{
    public DateTime CachedAt { get; set; }
    public List<CachedRelease> Releases { get; set; } = new();
}

public sealed class CachedRelease
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Flavor { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTime? ReleaseDate { get; set; }
    public string DownloadPageUrl { get; set; } = string.Empty;
    public string NewsUrl { get; set; } = string.Empty;
}
