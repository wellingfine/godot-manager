using System;
using System.Reflection;

namespace GodotManager.Services;

/// <summary>
/// 关于窗口展示的项目信息，集中在这里便于修改。
/// 作者取自 git config user.name，仓库地址取自 origin remote。
/// </summary>
public static class AppInfo
{
    public const string Author = "v0tx";
    public const string RepositoryUrl = "https://github.com/wellingfine/godot-manager";
    public const string RepositoryDisplay = "github.com/wellingfine/godot-manager";

    public static string Version => Assembly.GetExecutingAssembly().GetName().Version is { } version
        ? $"v{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}"
        : "v1.0.0";
}
