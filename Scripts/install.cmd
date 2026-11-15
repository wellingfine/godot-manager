@echo off
rem Called by the self-extracting installer, or run manually. Forwards to install.ps1.
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
exit /b %errorlevel%
 
