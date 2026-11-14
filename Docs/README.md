# Godot Manager 开发文档

本目录面向想理解或改动本项目的开发者。普通使用说明见根目录 [readme.md](../readme.md)。

## 快速开始

```bash
dotnet restore
dotnet build
dotnet run
```

## 文档导航

| 文档 | 内容 |
| --- | --- |
| [architecture.md](architecture.md) | 技术栈、分层、模块职责、数据流、状态持久化、设计取舍与限制 |
| [development.md](development.md) | 开发环境、目录结构、编码约定、常见改动点、调试与验证清单 |
| [build.md](build.md) | Debug / Release 构建、`publish` 产物、发布注意事项、常见问题 |

## 一眼看懂

- UI：Avalonia 12 + XAML（编译绑定）；MVVM 用 `CommunityToolkit.Mvvm`
- 国际化：中文 / English，右上角设置图标里切换，立即生效；界面配置全部持久化到 `state.json`
- 版本数据：抓取 `godotengine.org/download/archive/` 的静态 HTML，正则解析；成功后缓存到 `releases-cache.json`（默认 2 天），刷新 30 秒内走本地缓存
- 下载：`downloads.godotengine.org` 官方直链，流式下载到 `<安装目录>\downloads`，再解压到安装目录
- 已安装版本：启动时扫描安装目录 + 本地 `state.json` 记录
- 配置：`%LOCALAPPDATA%\GodotManager\state.json`
- **当前只支持 Windows 64 位编辑器**（标准版 / .NET Mono 版），平台限制见 [architecture.md](architecture.md#平台支持现状)
