@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ============================================================
rem  Godot Version Manager - Windows packaging script
rem
rem  NOTE: keep this file ASCII-only. Changing the console code page
rem        from inside a .bat makes cmd.exe misread the rest of the
rem        file when it contains multibyte text.
rem
rem  Usage:
rem    Scripts\package.bat                self-contained win-x64 build
rem    Scripts\package.bat -r win-arm64   set runtime identifier
rem    Scripts\package.bat -v 0.3.0       set version, default: [Version] from csproj
rem    Scripts\package.bat -o D:\out      set output dir, default: [repo]\dist
rem    Scripts\package.bat -f             framework-dependent build, .NET 10 runtime required on target
rem    Scripts\package.bat --no-zip       publish only, skip compression
rem    Scripts\package.bat --no-clean     keep the previous publish directory
rem    Scripts\package.bat --no-setup     skip the self-extracting installer, built by default
rem    Scripts\package.bat -h             show this help
rem
rem  Output, all under dist:
rem    dist\GodotManager-[version]-[rid]\      publish folder, exe + native DLLs, ship the whole folder
rem    dist\GodotManager-[version]-[rid].zip  archive
rem    dist\GodotManager-[version]-[rid]-setup.exe  self-extracting installer
rem ============================================================

rem PKG_ prefix avoids collisions with MSBuild built-in properties
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
rem 7-Zip is used to build the self-extracting installer
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
echo [ERROR] Unknown option: %~1
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
echo  Godot Version Manager - Package
echo ============================================
echo   Version        : %PKG_VERSION%
echo   RID            : %PKG_RID%
echo   Self-contained : %PKG_SELF_CONTAINED%
echo   Publish dir    : %PKG_PUBDIR%
if "%PKG_MAKE_ZIP%"=="true" echo   Archive        : %PKG_ZIP%
echo.

if "%PKG_DO_CLEAN%"=="true" call :cleanPub
if not exist "%PKG_PUBDIR%" mkdir "%PKG_PUBDIR%"
if not exist "%PKG_OUTDIR%" mkdir "%PKG_OUTDIR%"

echo [1/3] dotnet publish ...
"%PKG_DOTNET%" publish "%PKG_PROJ%" -c Release -r %PKG_RID% --self-contained %PKG_SELF_CONTAINED% -p:PublishSingleFile=true -p:DebugType=none -p:PublishTrimmed=false -o "%PKG_PUBDIR%" --nologo
if errorlevel 1 goto publishFailed

echo [2/3] Copying readme ...
if exist "%PKG_ROOT%\readme.md" copy /y "%PKG_ROOT%\readme.md" "%PKG_PUBDIR%\README.md" >nul
rem Runtime PDBs are large, do not ship them
del /q "%PKG_PUBDIR%\*.pdb" 2>nul

if "%PKG_MAKE_ZIP%"=="true" goto doZip
echo [3/3] Skipping compression.
goto zipDone

:doZip
echo [3/3] Compressing ...
if exist "%PKG_ZIP%" del /q "%PKG_ZIP%"
tar -a -c -f "%PKG_ZIP%" -C "%PKG_PUBDIR%" . >nul 2>&1
if not errorlevel 1 goto zipOk
echo       tar unavailable, falling back to PowerShell Compress-Archive ...
powershell -NoProfile -Command "Compress-Archive -Path '%PKG_PUBDIR%\*' -DestinationPath '%PKG_ZIP%' -Force"
if errorlevel 1 goto zipFailed
:zipOk
goto zipDone

:zipFailed
echo [ERROR] Compression failed.
exit /b 1

:zipDone
if "%PKG_SETUP%"=="true" call :makeSetup
echo.
echo Done.
if "%PKG_MAKE_ZIP%"=="true" call :showZip
echo   Publish dir: %PKG_PUBDIR%
echo   Note: Avalonia native dependencies such as libSkiaSharp.dll are not merged into the exe; ship the whole folder.
echo.
endlocal
exit /b 0

rem Build the self-extracting installer: zip the publish folder as payload, then publish Tools/Setup
:makeSetup
set "PKG_SETUPEXE=%PKG_OUTDIR%\GodotManager-%PKG_VERSION%-%PKG_RID%-setup.exe"
set "PKG_LOADDIR=%PKG_OUTDIR%\setup-payload"
set "PKG_PAYLOAD=%PKG_ROOT%\Tools\Setup\Payload.zip"
set "PKG_SETUPDIR=%PKG_OUTDIR%\setup-build"

rem 1) Assemble the payload
if exist "%PKG_LOADDIR%" rd /s /q "%PKG_LOADDIR%"
mkdir "%PKG_LOADDIR%"
xcopy "%PKG_PUBDIR%\*" "%PKG_LOADDIR%\" /E /Q /Y >nul
copy /y "%PKG_ROOT%\Scripts\install.ps1" "%PKG_LOADDIR%\" >nul
copy /y "%PKG_ROOT%\Scripts\uninstall.ps1" "%PKG_LOADDIR%\" >nul
echo %PKG_VERSION%> "%PKG_LOADDIR%\version.txt"

rem 2) Zip the payload
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

rem 3) Publish the single-file installer
if exist "%PKG_SETUPDIR%" rd /s /q "%PKG_SETUPDIR%"
"%PKG_DOTNET%" publish "%PKG_ROOT%\Tools\Setup\Setup.csproj" -c Release -r %PKG_RID% -o "%PKG_SETUPDIR%" --nologo
if errorlevel 1 goto setupFailed
if exist "%PKG_SETUPEXE%" del /q "%PKG_SETUPEXE%"
copy /y "%PKG_SETUPDIR%\GodotManager-Setup.exe" "%PKG_SETUPEXE%" >nul
if errorlevel 1 goto setupFailed

rd /s /q "%PKG_LOADDIR%"
rd /s /q "%PKG_SETUPDIR%"
del /q "%PKG_PAYLOAD%"
echo   Installer: %PKG_SETUPEXE%
goto :eof

:setupFailed
echo [ERROR] Failed to build the installer.
exit /b 1

:readVersion
for /f "tokens=3 delims=<>" %%v in ('findstr /c:"<Version>" "%PKG_PROJ%"') do set "PKG_VERSION=%%v"
goto :eof

:cleanPub
if exist "%PKG_PUBDIR%" rd /s /q "%PKG_PUBDIR%"
goto :eof

:showZip
for %%F in ("%PKG_ZIP%") do set "PKG_ZIPSIZE=%%~zF"
echo   Archive: %PKG_ZIP% [%PKG_ZIPSIZE% bytes]
goto :eof

:noProj
echo [ERROR] Project file not found: %PKG_PROJ%
exit /b 1

:noDotnet
echo [ERROR] dotnet not found. Install the .NET 10 SDK and add it to PATH.
exit /b 1

:noVersion
echo [ERROR] Cannot read version from %PKG_PROJ%; use -v to set it manually.
exit /b 1

:publishFailed
echo [ERROR] Publish failed.
exit /b 1

:usage
echo.
echo Usage: Scripts\package.bat [options]
echo.
echo   -r, --rid [rid]            Runtime identifier, default win-x64
echo   -v, --version [ver]        Version, default: [Version] from GodotManager.csproj
echo   -o, --output [dir]         Output directory, default [repo]\dist
echo   -f, --framework-dependent  Framework-dependent build, .NET 10 runtime required on target
echo   --no-zip                  Publish only, skip compression
echo   --no-clean                Keep the previous publish directory
echo   --no-setup                Skip the self-extracting installer, built by default
echo   -h, --help                 Show this help
echo.
echo Examples:
echo   Scripts\package.bat
echo   Scripts\package.bat -v 0.3.0 -r win-x64
echo   Scripts\package.bat -f --no-zip
echo.

endlocal
exit /b 0
