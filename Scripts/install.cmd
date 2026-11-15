@echo off
rem 由自解压安装包调用（也可手动运行）：把程序安装到 %LOCALAPPDATA%\GodotManager\app
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
exit /b %errorlevel%
 
