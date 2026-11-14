using System;
using System.IO;
using System.Text.Json;
using GodotManager.Models;

namespace GodotManager.Services;

public sealed class AppStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _statePath;

    public AppState State { get; }

    public AppStateService()
    {
        Directory.CreateDirectory(AppState.DataDirectory);
        _statePath = Path.Combine(AppState.DataDirectory, "state.json");
        State = Load();
        try
        {
            Directory.CreateDirectory(State.InstallDirectory);
        }
        catch (Exception)
        {
            // 配置里的目录不可用时回退到默认目录，避免程序无法启动。
            State.InstallDirectory = AppState.DefaultInstallDirectory;
            Directory.CreateDirectory(State.InstallDirectory);
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(State, JsonOptions);
        File.WriteAllText(_statePath, json);
    }

    private AppState Load()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                var json = File.ReadAllText(_statePath);
                var state = JsonSerializer.Deserialize<AppState>(json, JsonOptions) ?? new AppState();

                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty(nameof(AppState.ShowStable), out _))
                {
                    // 旧配置没有 stable 开关：迁移到「前三种默认勾选」，避免旧的 ShowRc=false 覆盖新默认值。
                    state.ShowStable = true;
                    state.ShowRc = true;
                    state.ShowBeta = true;
                    state.ShowDev = false;
                    state.ShowAlpha = false;
                    File.WriteAllText(_statePath, JsonSerializer.Serialize(state, JsonOptions));
                }

                return state;
            }
        }
        catch (Exception)
        {
            // 配置损坏时回退到默认状态，不影响程序启动。
        }

        return new AppState();
    }
}
