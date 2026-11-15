# 开发指南

## 环境要求

- **.NET 10 SDK**（本项目目标框架 `net10.0`；开发时使用 10.0.401）
- 可访问 NuGet（`api.nuget.org`）与 `godotengine.org`（版本列表、下载重定向）
- 推荐 IDE：Visual Studio 2026 / JetBrains Rider / VS Code + C# Dev Kit，配合 Avalonia 扩展可获得 XAML 预览与补全
- Windows 为主；macOS / Linux 能编译运行 UI，但功能受平台限制（见 [architecture.md](architecture.md#平台支持现状)）

## 运行与调试

```bash
dotnet run                 # 开发时最常用
dotnet build               # 只编译
```

- `Debug` 构建在 `Program.cs` 里启用了 `.WithDeveloperTools()`，运行时按 `F12` 打开 Avalonia 开发者工具查看可视化树与绑定。
- `.LogToTrace()` 把 Avalonia 内部日志输出到调试器 / 跟踪输出。
- 业务错误（网络失败、下载失败、启动失败、卸载失败）统一显示在主窗口底部状态栏 `Status`，没有引入日志框架。想排查问题时先看状态栏。
- 配置与实际效果可直接查看 `%LOCALAPPDATA%\GodotManager\state.json`（修改后需重启程序生效）。版本列表缓存在同目录的 `releases-cache.json`，删除该文件即可强制下次启动重新联网拉取。

> 修改代码前先关闭正在运行的 `GodotManager.exe`，否则 Windows 会锁定输出文件导致 `MSB3026 / MSB3027` 构建失败。

## 编码约定

- 项目**未启用** `ImplicitUsings`，每个 `.cs` 文件显式写 `using`。
- 使用 file-scoped namespace：`namespace GodotManager.Services;`。
- 启用 nullable，异步方法统一 `async Task`，取消用 `CancellationTokenSource` + `OperationCanceledException`。
- ViewModel 状态用 `[ObservableProperty]` 字段，命令用 `[RelayCommand]`；属性变化后的联动写 `partial void OnXxxChanged(...)`。
- 命令的可用性用 `[RelayCommand(CanExecute = nameof(...))]` 配合 `[NotifyCanExecuteChangedFor(...)]`。
- 耗时操作（解压 `ZipFile`、目录扫描）放 `Task.Run`，UI 进度通过 `Progress<T>` 回到 UI 线程。
- **Avalonia 12 默认编译绑定**：`Window` 必须声明 `x:DataType`；每个 `DataTemplate` 必须声明 `DataType="{x:Type models:...}"`。漏写会得到 `AVLN2100` / `AVLN2000` 编译错误。
- 新增 XAML 控件绑定前，确认对应属性在 VM 上存在且可访问；编译期就能发现拼写错误。
- 不要提交 `bin/`、`obj/`、`publish/`，`.gitignore` 已覆盖。

## 常见改动点

| 想做的事 | 改哪里 |
| --- | --- |
| 增加 macOS / Linux 支持 | `DownloadService.BuildDownloadUrl`、`InstallService`（`ExecutableRegex` / `Scan` / `Install` / `GetTempZipPath`）、`MainWindowViewModel.Launch`；详见 [architecture.md](architecture.md#平台支持现状) |
| 更换版本数据源 / 适配官网改版 | `GodotArchiveService`（`ArchiveUrl`、`ReleaseRegex`、`NewsRegex`、`Parse`、`ParseDate`） |
| 调整排序或筛选 | `GodotVersionComparer`（在 `GodotArchiveService.cs` 内）、`MainWindowViewModel.ApplyFilter`、`RebuildMajorGroups`、`RefreshInstalled` |
| 改临时文件保留策略 / 命名 | `AppState.TempFileRetentionDays`、`InstallService.GetTempZipPath`、`CleanupExpiredDownloads` |
| 改缓存有效期 / 刷新间隔 | `AppState.CacheExpirationDays` / `RefreshCooldownSeconds`、`ReleaseCacheService`、`MainWindowViewModel` 的 `LoadReleasesAsync` / `RefreshReleasesAsync` / `FetchFromNetworkAsync` |
| 增加 / 修改界面文案 | `Services/LocalizationService.cs`：新增 XAML 文本要同时加属性与 `Zh` / `En` 两个字典条目；带参数文案用 `F(key, args)` 格式化模板。例外：设置窗口的语言下拉项固定写 `中文` / `English`（用语言自身的名称，不翻译） |
| 增加设置项 | `SettingsWindow.axaml(.cs)` + `AppState` 字段 + `MainWindowViewModel` 的 `Index` 属性与 `partial void OnXxxChanged` 钩子（变更时立即 `Save()`） |
| 改「打开链接 / 目录」的平台行为 | `Services/ShellService.cs` 的 `CreateOpenCommand()`（Windows / Mac / Linux 分支）；调用方是 `OpenArchiveUrl` / `OpenInstallDirectory` / `OpenFolder` 命令 |
| 改主配色 / UI 样式 | `App.axaml`：`Application.Resources` 里的 `SystemAccentColor`、`Brand*` 与 `ListItem*` 画刷；`Application.Styles` 里的 `card` / `panel` / `primary` / `icon` / `h2` / `muted` / `ListBoxItem` / `rowAction` 全局样式（列表项 hover / 选中圆角、行内操作区显隐），各窗口共用；窗口背景使用主题默认色 |
| 改作者 / 仓库地址 / 版本号 | `Services/AppInfo.cs`（`Author` / `RepositoryUrl` / `RepositoryDisplay`）；版本号来自 `GodotManager.csproj` 的 `<Version>`，由 `AppInfo.Version` 读取程序集版本 |
| 列表增加字段 | 对应 `Models/*` → `MainWindow.axaml` 的 `DataTemplate`（注意 `DataType`） |
| 增加新的持久化设置 | `AppState` 加属性 → `AppStateService` 自动序列化 → VM 加 `[ObservableProperty]` 与变更时 `Save()` |
| 增加新的界面命令 | VM 加 `[RelayCommand]` 方法 → XAML 绑定生成的 `XxxCommand` |
| 换下载服务器 / 加镜像 | `DownloadService.BuildDownloadUrl` 与 `CreateClient`（UA、超时） |
| 换程序 / 窗口图标 | 改根目录 `Icon.svg` 后重新导出 `Assets/Icon.ico`（多尺寸 ico），`GodotManager.csproj` 的 `<ApplicationIcon>` 已指向它；详见 [build.md](build.md#程序图标) |

### 加一个平台的最小改造顺序

1. 在服务层引入「平台描述」（下载 slug / platform、包文件名、可执行文件名或 `.app` 路径），用 `OperatingSystem.IsWindows() / IsMacOS() / IsLinux()` 选择。
2. 让 `Install()` 与 `Scan()` 共用同一个「定位可执行文件」函数，避免出现两套识别规则。
3. 把 `Launch()` 换成平台对应的启动方式（`Process.Start` / `open -a` / 加可执行权限后直接执行）。
4. 补文档与手动验证清单，再考虑 `.app` bundle 或 Linux 打包。

## 手动验证清单

目前没有单元测试项目，改动后建议按下列步骤回归：

1. `dotnet build` 无错误无警告（含 XAML 编译）。
2. `dotnet run`，状态栏显示已获取版本数量；左侧竖向 tab 出现 `全部 / 4.x / 3.x / 2.x / 1.x`，切换 tab 后列表随之筛选。
3. 搜索关键字、切换 Stable / RC / Beta / Dev / Alpha 复选框、刷新，确认排序始终按版本号降序、选中 tab 保留；默认勾前三个（Stable / RC / Beta），Dev / Alpha 不勾。
4. 指定一个含已有 Godot 目录的安装目录，确认「已安装」列出这些版本（含手动解压的、`.NET` 版与标准版）。
5. 下载一个体积较小的版本（如 `4.0-stable` 约 49 MB），检查进度、取消、`downloads` 目录中的 zip、解压目录与主 exe。
6. 在设置窗口把安装包策略分别切到「保留 30 天」与「安装完成后立即删除」各下一次，确认 zip 保留 / 删除行为；构造一个 40 天前的 `.zip` 放进 `downloads`，重启程序确认被清理，而新 zip 与安装目录根目录下的 zip 保留。
7. 「启动」能拉起 Godot，「打开目录」能打开资源管理器，「卸载」弹出确认并只删除对应版本目录。
8. 重启程序确认 `state.json` 中的记录与界面一致。
9. 删除 `releases-cache.json` 后启动，确认联网拉取并重新生成缓存；再次启动应从缓存加载；连续点两次刷新图标，第二次状态栏应提示「距上次刷新不足 30 秒，已从本地缓存解析」。
10. 打开右上角设置图标：中文 / English 切换后标题、按钮、tab、状态栏应立即换语言；改安装目录与安装包策略后关闭再打开保持一致；重启程序确认 `.NET 版`、Beta / Dev 勾选、主版本 tab 仍是上次状态。
11. 点击副标题里的官网链接应打开浏览器；点击设置窗口里的安装目录应打开文件浏览器；核对 `ShellService.CreateOpenCommand()` 在 Windows / Mac / Linux 分别生成 `explorer.exe` / `open` / `xdg-open`（Windows 打开 URL 走 `UseShellExecute`）。
12. 勾选 / 取消「.NET 版」，确认在线版本列表项的蓝色 `.NET` 标签同步出现 / 消失；已安装列表名称只显示版本号，第二行显示 `.NET (Mono)` / `标准版`，不重复。
