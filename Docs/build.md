# 构建与发布

## 前置条件

- .NET 10 SDK（含 `net10.0` 目标包）
- NuGet 可访问，首次构建会自动还原依赖
- Windows 上构建 / 运行前，关闭正在运行的 `GodotManager.exe`

依赖由 `GodotManager.csproj` 固定版本：

| 包 | 版本 | 说明 |
| --- | --- | --- |
| `Avalonia` / `Avalonia.Desktop` / `Avalonia.Themes.Fluent` / `Avalonia.Fonts.Inter` | 12.1.6 | UI 框架、桌面后端、主题、字体 |
| `CommunityToolkit.Mvvm` | 8.4.2 | MVVM 源生成 |
| `AvaloniaUI.DiagnosticsSupport` | 2.2.3 | 包始终引用，`WithDeveloperTools()` 仅在 `DEBUG` 条件编译下调用（`F12` 开发者工具） |

## 常用命令

```bash
dotnet restore                        # 还原 NuGet 依赖（build 会自动执行）
dotnet build                          # Debug 构建
dotnet build -c Release               # Release 构建
dotnet run                            # 构建并启动（Debug）
dotnet clean                          # 清理 bin / obj 中的构建产物
```

输出位置：

| 命令 | 产物 |
| --- | --- |
| `dotnet build` | `bin/Debug/net10.0/GodotManager.exe` + `GodotManager.dll` 等 |
| `dotnet build -c Release` | `bin/Release/net10.0/...` |
| `dotnet publish ...` | 指定输出目录，见下 |

项目是 `OutputType=WinExe` 的框架依赖应用，运行时机器需要安装 .NET 10 运行时；`app.manifest` 是 Windows 应用清单。

## 发布

生成可分发的目录（框架依赖，需目标机器有 .NET 10 运行时）：

```bash
dotnet publish -c Release -o bin/publish
```

生成 Windows x64 自包含单文件（无需目标机器装运行时，体积较大）：

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o bin/publish-win
```

实测产物：`GodotManager.exe` 约 85 MB，但 Avalonia 的原生依赖（`libSkiaSharp.dll`、`libHarfBuzzSharp.dll`、`av_libglesv2.dll` 等）**不会**被打进单文件，需要整个目录一起分发，不能只拷贝 exe。

注意事项：

- 上面的 `dotnet build -c Release` 与两条 `publish` 命令均在项目上验证通过；若加了 `-p:PublishTrimmed=true` 或 NativeAOT，需重新验证 Avalonia 的 XAML / 反射加载是否正常，本项目未启用裁剪。
- 发布产物不包含 `state.json` 和任何 Godot 版本文件；用户首次运行才会在 `%LOCALAPPDATA%\GodotManager\` 下创建配置。
- `bin/`、`obj/`、`publish/` 均被 `.gitignore` 忽略，不要提交构建产物。

## 打包一个可分发版本

先发布，再压缩成单个 zip（排除 `.pdb` 可显著减小体积）：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o bin/publish-win
$files = Get-ChildItem -LiteralPath bin/publish-win -File | Where-Object { $_.Extension -ne '.pdb' } | ForEach-Object { $_.FullName }
Compress-Archive -Path $files -DestinationPath bin/GodotManager-win-x64.zip -Force
```

产物是 `bin/GodotManager-win-x64.zip`：解压后**整目录分发**（主 exe + 原生 DLL），不要把 exe 单独拷出去。目标机器无需安装 .NET 运行时。

## 跨平台构建

```bash
dotnet publish -c Release -r osx-arm64 --self-contained true -o bin/publish-osx-arm64
dotnet publish -c Release -r linux-x64 --self-contained true -o bin/publish-linux-x64
```

`UsePlatformDetect()` 让 Avalonia UI 可以在 macOS / Linux 启动，但**功能仍是 Windows 专用**（下载参数、exe 扫描、启动方式），上述命令只解决「能跑起来」。另外 `WinExe` + Windows 清单不会产生正规的 macOS `.app` bundle，正式分发需要额外打包步骤。改造点见 [architecture.md](architecture.md#平台支持现状)。

## 常见问题

| 现象 | 原因 / 处理 |
| --- | --- |
| `MSB3026` / `MSB3027`：无法复制 `GodotManager.exe`，文件被占用 | 程序还在运行，关闭进程后重新构建 |
| `AVLN2100`：Cannot parse a compiled binding without an explicit `x:DataType` | `Window` 缺 `x:DataType`，或 `DataTemplate` 缺 `DataType` |
| `AVLN2000`：Unable to resolve property ... on type `XamlPseudoType` | 模板内绑定类型未声明，给 `DataTemplate` 加 `DataType="{x:Type ...}"` |
| `AVLN5001`：`TextBox.Watermark` obsolete | Avalonia 12 用 `PlaceholderText` |
| `NU1301` / 还原失败 | 检查 NuGet 网络与代理 |
| 界面版本列表为空 | 官网 HTML 结构变化导致正则失配，或网络不通；看状态栏错误，改 `GodotArchiveService.ReleaseRegex` |
| 发布后列表为空但已安装版本消失 | 检查目标机器的 `state.json` 与 `InstallDirectory` 是否指向有效目录 |
| Mac / Linux 上能开窗口但下载的东西用不了 | 尚未实现平台适配，见上文 |

## 回归检查

发布前至少跑一遍：

```bash
dotnet build -c Release
dotnet publish -c Release -o bin/publish
```

再按 [development.md](development.md#手动验证清单) 手动回归核心流程。
