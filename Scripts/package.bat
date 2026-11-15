@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul 2>&1

rem ============================================================
rem  Godot 版本管理器 - Windows 打包脚本
rem
rem  用法:
rem    Scripts\package.bat                打包 win-x64 自包含版
rem    Scripts\package.bat -r win-arm64   指定运行时标识
rem    Scripts\package.bat -v 0.3.0       指定版本号（默认取 csproj 的 <Version>）
rem    Scripts\package.bat -o D:\out      指定输出目录（默认 <仓库>\dist）
rem    Scripts\package.bat -f             框架依赖版（目标机器需自行安装 .NET 10 运行时）
rem    Scripts\package.bat --no-zip       只发布，不压缩
rem    Scripts\package.bat --no-clean     不清空旧的发布目录
rem    Scripts\package.bat --no-setup      不生成自解压安装包（默认会生成）
rem    Scripts\package.bat -h             查看帮助
rem
rem  产物（都在 dist 下）:
rem    dist\GodotManager-<版本>-<rid>\      发布目录（exe + 原生依赖 DLL，整个目录一起分发）
rem    dist\GodotManager-<版本>-<rid>.zip   压缩包
rem    dist\GodotManager-<版本>-<rid>-setup.exe  自解压安装包（默认生成，--no-setup 可跳过）
rem ============================================================

rem 变量统一加 PKG_ 前缀，避免与 MSBuild 的内置属性（OutDir / Version / RID 等）冲突
set "PKG_ROOT=%~dp0.."
for %%i in ("%PKG_ROOT%") do set "PKG_ROOT=%%~fi"
set "PKG_PROJ=%PKG_ROOT%\GodotManager.csproj"
set "PKG_RID=win-x64"
set "PKG_OUTDIR=%PKG_ROOT%\dist"
set "PKG_VERSION="
set "PKG_SELF_CONTAINED=true"
set "PKG_MAKE_ZIP=true"
set "PKG_DO_CLEAN=true"
set "PKG_SETUP=true"
set "PKG_DOTNET=dotnet"
rem 7-Zip（用于合成自解压安装包）
set "PKG_SEVENZIP="
if exist "%ProgramFiles%\7-Zip\7z.exe" set "PKG_SEVENZIP=%ProgramFiles%\7-Zip\7z.exe"
if not defined PKG_SEVENZIP if exist "%ProgramFiles(x86)%\7-Zip\7z.exe" set "PKG_SEVENZIP=%ProgramFiles(x86)%\7-Zip\7z.exe"

:parseArgs
if "%~1"=="" goto afterArgs
if /i "%~1"=="-h" goto usage
if /i "%~1"=="--help" goto usage
if /i "%~1"=="-v" goto argVersion
if /i "%~1"=="--version" goto argVersion
if /i "%~1"=="-r" goto argRid
if /i "%~1"=="--rid" goto argRid
if /i "%~1"=="-o" goto argOutput
if /i "%~1"=="--output" goto argOutput
if /i "%~1"=="-f" goto argFd
if /i "%~1"=="--framework-dependent" goto argFd
if /i "%~1"=="--no-zip" goto argNoZip
if /i "%~1"=="--no-clean" goto argNoClean
if /i "%~1"=="--no-setup" goto argNoSetup
if /i "%~1"=="--setup" goto argSetup
if /i "%~1"=="--installer" goto argSetup
echo [错误] 无法识别的参数: %~1
echo.
goto usage

:argVersion
set "PKG_VERSION=%~2"
shift
shift
goto parseArgs

:argRid
set "PKG_RID=%~2"
shift
shift
goto parseArgs

:argOutput
set "PKG_OUTDIR=%~2"
shift
shift
goto parseArgs

:argFd
set "PKG_SELF_CONTAINED=false"
shift
goto parseArgs

:argNoZip
set "PKG_MAKE_ZIP=false"
shift
goto parseArgs

:argNoClean
set "PKG_DO_CLEAN=false"
shift
goto parseArgs

:argSetup
set "PKG_SETUP=true"
shift
goto parseArgs

:argNoSetup
set "PKG_SETUP=false"
shift
goto parseArgs

:afterArgs
if not exist "%PKG_PROJ%" goto noProj
where "%PKG_DOTNET%" >nul 2>&1
if errorlevel 1 goto noDotnet
if defined PKG_VERSION goto versionReady
call :readVersion
:versionReady
set "PKG_VERSION=%PKG_VERSION: =%"
if not defined PKG_VERSION goto noVersion

set "PKG_PUBDIR=%PKG_OUTDIR%\GodotManager-%PKG_VERSION%-%PKG_RID%"
set "PKG_ZIP=%PKG_OUTDIR%\GodotManager-%PKG_VERSION%-%PKG_RID%.zip"

echo.
echo ============================================
echo  Godot 版本管理器 打包
echo ============================================
echo   版本          : %PKG_VERSION%
echo   运行时标识    : %PKG_RID%
echo   发布模式      : %PKG_SELF_CONTAINED%
echo   发布目录      : %PKG_PUBDIR%
if "%PKG_MAKE_ZIP%"=="true" echo   压缩包        : %PKG_ZIP%
echo.

if "%PKG_DO_CLEAN%"=="true" call :cleanPub
if not exist "%PKG_PUBDIR%" mkdir "%PKG_PUBDIR%"
if not exist "%PKG_OUTDIR%" mkdir "%PKG_OUTDIR%"

echo [1/3] dotnet publish ...
"%PKG_DOTNET%" publish "%PKG_PROJ%" -c Release -r %PKG_RID% --self-contained %PKG_SELF_CONTAINED% -p:PublishSingleFile=true -p:DebugType=none -p:PublishTrimmed=false -o "%PKG_PUBDIR%" --nologo
if errorlevel 1 goto publishFailed

echo [2/3] 复制说明文件 ...
if exist "%PKG_ROOT%\readme.md" copy /y "%PKG_ROOT%\readme.md" "%PKG_PUBDIR%\README.md" >nul
rem 运行时包自带的 pdb 体积很大，不随包分发
del /q "%PKG_PUBDIR%\*.pdb" 2>nul

if "%PKG_MAKE_ZIP%"=="true" goto doZip
echo [3/3] 跳过压缩。
goto zipDone

:doZip
echo [3/3] 压缩 ...
if exist "%PKG_ZIP%" del /q "%PKG_ZIP%"
tar -a -c -f "%PKG_ZIP%" -C "%PKG_PUBDIR%" . >nul 2>&1
if not errorlevel 1 goto zipOk
echo       tar 不可用，改用 PowerShell Compress-Archive ...
powershell -NoProfile -Command "Compress-Archive -Path '%PKG_PUBDIR%\*' -DestinationPath '%PKG_ZIP%' -Force"
if errorlevel 1 goto zipFailed
:zipOk
goto zipDone

:zipFailed
echo [错误] 压缩失败。
exit /b 1

:zipDone
if "%PKG_SETUP%"=="true" call :makeSetup
echo.
echo 打包完成。
if "%PKG_MAKE_ZIP%"=="true" call :showZip
echo   发布目录: %PKG_PUBDIR%
echo   注意: Avalonia 的原生依赖（libSkiaSharp.dll 等）不会被合并进 exe，需整个目录一起分发。
echo.
endlocal
exit /b 0

rem 生成自解压安装包：把发布目录打成载荷，再发布 Tools/Setup 里的单文件安装器
:makeSetup
set "PKG_SETUPEXE=%PKG_OUTDIR%\GodotManager-%PKG_VERSION%-%PKG_RID%-setup.exe"
set "PKG_LOADDIR=%PKG_OUTDIR%\setup-payload"
set "PKG_PAYLOAD=%PKG_ROOT%\Tools\Setup\Payload.zip"
set "PKG_SETUPDIR=%PKG_OUTDIR%\setup-build"

rem 1) 组装载荷
if exist "%PKG_LOADDIR%" rd /s /q "%PKG_LOADDIR%"
mkdir "%PKG_LOADDIR%"
xcopy "%PKG_PUBDIR%\*" "%PKG_LOADDIR%\" /E /Q /Y >nul
copy /y "%PKG_ROOT%\Scripts\install.ps1" "%PKG_LOADDIR%\" >nul
copy /y "%PKG_ROOT%\Scripts\uninstall.ps1" "%PKG_LOADDIR%\" >nul
echo %PKG_VERSION%> "%PKG_LOADDIR%\version.txt"

rem 2) 压成 Payload.zip
if exist "%PKG_PAYLOAD%" del /q "%PKG_PAYLOAD%"
if defined PKG_SEVENZIP goto zipWith7z
powershell -NoProfile -Command "Compress-Archive -Path '%PKG_LOADDIR%\*' -DestinationPath '%PKG_PAYLOAD%' -Force"
goto setupZipDone
:zipWith7z
pushd "%PKG_LOADDIR%"
"%PKG_SEVENZIP%" a -tzip -mx=6 "%PKG_PAYLOAD%" * >nul
popd
:setupZipDone
if not exist "%PKG_PAYLOAD%" goto setupFailed

rem 3) 发布单文件安装器
if exist "%PKG_SETUPDIR%" rd /s /q "%PKG_SETUPDIR%"
"%PKG_DOTNET%" publish "%PKG_ROOT%\Tools\Setup\Setup.csproj" -c Release -r %PKG_RID% -o "%PKG_SETUPDIR%" --nologo
if errorlevel 1 goto setupFailed
if exist "%PKG_SETUPEXE%" del /q "%PKG_SETUPEXE%"
copy /y "%PKG_SETUPDIR%\GodotManager-Setup.exe" "%PKG_SETUPEXE%" >nul
if errorlevel 1 goto setupFailed

rd /s /q "%PKG_LOADDIR%"
rd /s /q "%PKG_SETUPDIR%"
del /q "%PKG_PAYLOAD%"
echo   安装包: %PKG_SETUPEXE%
goto :eof

:setupFailed
echo [错误] 生成安装包失败。
exit /b 1

:readVersion
for /f "tokens=3 delims=<>" %%v in ('findstr /c:"<Version>" "%PKG_PROJ%"') do set "PKG_VERSION=%%v"
goto :eof

:cleanPub
if exist "%PKG_PUBDIR%" rd /s /q "%PKG_PUBDIR%"
goto :eof

:showZip
for %%F in ("%PKG_ZIP%") do set "PKG_ZIPSIZE=%%~zF"
echo   压缩包: %PKG_ZIP%  (%PKG_ZIPSIZE% 字节)
goto :eof

:noProj
echo [错误] 找不到项目文件: %PKG_PROJ%
exit /b 1

:noDotnet
echo [错误] 未找到 dotnet，请先安装 .NET 10 SDK 并加入 PATH。
exit /b 1

:noVersion
echo [错误] 无法从 %PKG_PROJ% 读取版本号，请用 -v 手动指定。
exit /b 1

:publishFailed
echo [错误] 发布失败。
exit /b 1

:usage
echo.
echo 用法: Scripts\package.bat [选项]
echo.
echo   -r, --rid [rid]         运行时标识，默认 win-x64（可选 win-x64 / win-arm64 / linux-x64 / osx-arm64 ...）
echo   -v, --version [版本]     版本号，默认读取 GodotManager.csproj 的 [Version]
echo   -o, --output [目录]      输出目录，默认 [仓库]\dist
echo   -f, --framework-dependent  框架依赖发布（目标机器需自行安装 .NET 10 运行时）
echo   --no-zip                只发布，不压缩
echo   --no-clean              不清空旧的发布目录
echo   --no-setup              不生成自解压安装包（默认会生成）
echo   -h, --help              显示本帮助
echo.
echo 示例:
echo   Scripts\package.bat
echo   Scripts\package.bat -v 0.3.0 -r win-x64
echo   Scripts\package.bat -f --no-zip
echo.

endlocal
exit /b 0
