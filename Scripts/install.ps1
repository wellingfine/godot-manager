# Godot 版本管理器 —— 安装脚本（由自解压安装包调用，也可手动运行）
param(
    # 安装位置的「父目录」；不传则弹出文件夹选择对话框。
    # 程序实际会装到 <父目录>\GodotManager 下，不会直接动到你选中的目录。
    [string]$Target
)

$ErrorActionPreference = 'Stop'

$appName       = 'GodotManager'
$appTitle      = 'Godot 版本管理器'
$source        = Split-Path -Parent $MyInvocation.MyCommand.Path
$root          = Join-Path $env:LOCALAPPDATA $appName        # 用户配置所在（AppState.DataDirectory）
$installSubdir = $appName                                    # 程序固定装到 <父目录>\GodotManager（不再套一层 app）
$defaultParent = Join-Path $env:LOCALAPPDATA 'Programs'      # 默认装到 LOCALAPPDATA\Programs\GodotManager，避免和上面的配置目录撞车
$startMenu     = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$desktop       = [Environment]::GetFolderPath('Desktop')
$uninstallKey  = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $appName

Write-Host ''
Write-Host ('  ' + $appTitle + ' 安装程序')
Write-Host '  --------------------------'
Write-Host ''

# 0. 选择安装位置的「父目录」
$createDesktop = $true   # 非交互（-Target）模式默认创建桌面快捷方式
if ([string]::IsNullOrWhiteSpace($Target)) {
    Add-Type -AssemblyName System.Windows.Forms
    # 默认父目录可能还不存在，先建好，对话框才能正确预选
    if (-not (Test-Path $defaultParent)) {
        New-Item -ItemType Directory -Path $defaultParent -Force | Out-Null
    }
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = '选择 ' + $appTitle + ' 的安装位置（将在其下创建 GodotManager 目录）'
    $dialog.SelectedPath = $defaultParent
    $dialog.ShowNewFolderButton = $true
    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        Write-Host '  已取消安装。'
        exit 10
    }
    $parent = $dialog.SelectedPath
    $answer = [System.Windows.Forms.MessageBox]::Show(
        '是否创建桌面快捷方式？', $appTitle,
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Question)
    $createDesktop = ($answer -eq [System.Windows.Forms.DialogResult]::Yes)
} else {
    $parent = $Target.TrimEnd('\')
}
# 真正的安装目录：<父目录>\GodotManager（只删这个子目录，绝不会删到父目录）
$target = Join-Path $parent $installSubdir
$exe = Join-Path $target 'GodotManager.exe'

# 版本号由打包脚本写进载荷
$version = '0.0.0'
$versionFile = Join-Path $source 'version.txt'
if (Test-Path $versionFile) {
    $version = (Get-Content $versionFile -First 1).Trim()
}

# 确保卸载脚本目录存在（原设计放在 LOCALAPPDATA\GodotManager）
if (-not (Test-Path $root)) {
    New-Item -ItemType Directory -Path $root -Force | Out-Null
}

# 1. 复制程序文件（只清理我们自己的安装子目录，不影响父目录里其它东西）
if ($source.TrimEnd('\') -ne $target.TrimEnd('\')) {
    if (Test-Path $target) {
        Remove-Item -Recurse -Force $target -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    $skip = @('install.cmd', 'install.ps1', 'uninstall.ps1', 'version.txt')
    Get-ChildItem -LiteralPath $source -File |
        Where-Object { $skip -notcontains $_.Name } |
        ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $target $_.Name) -Force
        }
}
Write-Host ('  已安装到 ' + $target)

# 2. 卸载器（放在 root 下，卸载时保留用户配置）
Copy-Item -LiteralPath (Join-Path $source 'uninstall.ps1') -Destination (Join-Path $root 'uninstall.ps1') -Force
$uninstallCmd = Join-Path $root 'uninstall.cmd'
@('@echo off', 'powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"', 'pause') -join "`r`n" |
    Set-Content -LiteralPath $uninstallCmd -Encoding ASCII

# 3. 快捷方式（开始菜单始终创建；桌面按用户选择）
$shell = New-Object -ComObject WScript.Shell
function New-Shortcut([string]$path, [string]$icon) {
    $link = $shell.CreateShortcut($path)
    $link.TargetPath = $exe
    $link.WorkingDirectory = $target
    $link.Description = $appTitle
    $link.IconLocation = $icon
    $link.Save()
}
New-Shortcut (Join-Path $startMenu ($appTitle + '.lnk')) ($exe + ',0')
if ($createDesktop) {
    New-Shortcut (Join-Path $desktop ($appTitle + '.lnk')) ($exe + ',0')
}
New-Shortcut (Join-Path $startMenu ('卸载 ' + $appTitle + '.lnk')) $uninstallCmd

# 4. 注册到「应用和功能」
New-Item -Path $uninstallKey -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'DisplayName' -Value $appTitle -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'DisplayVersion' -Value $version -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'Publisher' -Value 'v0tx' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'InstallLocation' -Value $target -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'UninstallString' -Value ('"' + $uninstallCmd + '"') -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'DisplayIcon' -Value ($exe + ',0') -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'NoModify' -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'NoRepair' -Value 1 -PropertyType DWord -Force | Out-Null

if ($createDesktop) {
    Write-Host '  已创建开始菜单与桌面快捷方式'
} else {
    Write-Host '  已创建开始菜单快捷方式（未创建桌面快捷方式）'
}
Write-Host ''
Write-Host '  安装完成，正在启动...'
Write-Host ''
Start-Process -FilePath $exe
