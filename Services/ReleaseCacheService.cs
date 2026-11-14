using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GodotManager.Models;

namespace GodotManager.Services;

public sealed class ReleaseCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _cachePath;

    public ReleaseCacheService()
    {
        Directory.CreateDirectory(AppState.DataDirectory);
        _cachePath = Path.Combine(AppState.DataDirectory, "releases-cache.json");
    }

    public IReadOnlyList<GodotRelease>? Load(out DateTime cachedAt)
    {
        cachedAt = default;
        try
        {
            if (!File.Exists(_cachePath))
            {
                return null;
            }

            var cache = JsonSerializer.Deserialize<ReleaseCache>(File.ReadAllText(_cachePath), JsonOptions);
            if (cache is null || cache.Releases.Count == 0)
            {
                return null;
            }

            cachedAt = cache.CachedAt;
            return cache.Releases.Select(item => new GodotRelease
            {
                Id = item.Id,
                Version = item.Version,
                Flavor = item.Flavor,
                Channel = item.Channel,
                ReleaseDate = item.ReleaseDate,
                DownloadPageUrl = item.DownloadPageUrl,
                NewsUrl = item.NewsUrl
            }).ToList();
        }
        catch (Exception)
        {
            // 缓存损坏时当作没有缓存，重新联网即可。
            return null;
        }
    }

    public void Save(IReadOnlyList<GodotRelease> releases)
    {
        try
        {
            var cache = new ReleaseCache
            {
                CachedAt = DateTime.Now,
                Releases = releases.Select(item => new CachedRelease
                {
                    Id = item.Id,
                    Version = item.Version,
                    Flavor = item.Flavor,
                    Channel = item.Channel,
                    ReleaseDate = item.ReleaseDate,
                    DownloadPageUrl = item.DownloadPageUrl,
                NewsUrl = item.NewsUrl
                }).ToList()
            };

            File.WriteAllText(_cachePath, JsonSerializer.Serialize(cache, JsonOptions));
        }
        catch (Exception)
        {
            // 缓存只是加速手段，写失败不影响主流程。
        }
    }
}
