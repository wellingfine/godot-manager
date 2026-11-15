# Godot 版本管理器 —— 卸载脚本（由「应用和功能」或开始菜单的卸载项调用）
$ErrorActionPreference = 'SilentlyContinue'

$appName   = 'GodotManager'
$appTitle  = 'Godot 版本管理器'
$root      = Join-Path $env:LOCALAPPDATA $appName
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$desktop   = [Environment]::GetFolderPath('Desktop')

# 安装目录可能被用户改过，优先从注册表读取；读不到再退回默认位置
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $appName
$reg = Get-ItemProperty $uninstallKey -ErrorAction SilentlyContinue
if ($reg -and $reg.InstallLocation) {
    $target = $reg.InstallLocation.TrimEnd('\')
} else {
    $target = Join-Path (Join-Path $env:LOCALAPPDATA 'Programs') $appName
}

Write-Host ''
Write-Host ('  正在卸载 ' + $appTitle + ' ...')

# 先结束正在运行的程序
Get-Process -Name $appName -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# 只删程序文件，保留用户配置（state.json 等）
Remove-Item -Recurse -Force $target
Remove-Item -Force (Join-Path $startMenu ($appTitle + '.lnk'))
Remove-Item -Force (Join-Path $startMenu ('卸载 ' + $appTitle + '.lnk'))
Remove-Item -Force (Join-Path $desktop ($appTitle + '.lnk'))
Remove-Item -Path ('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $appName) -Recurse -Force
Remove-Item -Force (Join-Path $root 'uninstall.cmd')
Remove-Item -Force (Join-Path $root 'uninstall.ps1')

Write-Host '  已卸载完成。'
Write-Host ('  用户配置仍保留在 ' + $root)
Write-Host ''
 
