@echo off
rem 由自解压安装包调用（也可手动运行）：转发给 install.ps1 完成安装（安装目录由脚本中的选择对话框决定，默认 %LOCALAPPDATA%\Programs\GodotManager）
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
exit /b %errorlevel%
 
