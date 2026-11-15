using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

// Godot 版本管理器 —— 自解压安装程序
// 安装载荷（Payload.zip）以嵌入资源形式打进这个单文件 exe：
// 运行时先解压到临时目录，install.ps1 会弹出目录选择对话框，
// 把文件装到用户选的目录并建立快捷方式、注册卸载项、启动程序。

internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private const uint MbIconError = 0x00000010;

    private static int Main()
    {
        // 载荷先解压到临时目录，装完后在 finally 里清理
        var tempDir = Path.Combine(Path.GetTempPath(), "GodotManager-setup");

        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }

            Directory.CreateDirectory(tempDir);
            ExtractPayload(tempDir);

            var install = Path.Combine(tempDir, "install.ps1");
            if (!File.Exists(install))
            {
                throw new InvalidOperationException("安装载荷缺少 install.ps1");
            }

            var code = RunPowerShell(install);
            if (code == 0)
            {
                MessageBoxW(IntPtr.Zero, "安装完成。可在开始菜单或桌面找到「Godot 版本管理器」。", "Godot 版本管理器 安装程序", 0);
                return 0;
            }

            if (code == 10)
            {
                // 用户在目录选择对话框里点了取消
                return 0;
            }

            MessageBoxW(IntPtr.Zero, "安装失败（退出码 " + code + "），详情见安装窗口。", "Godot 版本管理器 安装程序", MbIconError);
            return 1;
        }
        catch (Exception ex)
        {
            MessageBoxW(IntPtr.Zero, "安装失败：\r\n" + ex.Message, "Godot 版本管理器 安装程序", MbIconError);
            return 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch
            {
                // 临时目录清理失败不影响安装结果
            }
        }
    }

    private static void ExtractPayload(string targetDir)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("GodotManager.Setup.Payload.zip")
            ?? throw new InvalidOperationException("安装载荷缺失（Payload.zip）");
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            entry.ExtractToFile(Path.Combine(targetDir, entry.Name), true);
        }
    }

    private static int RunPowerShell(string script)
    {
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(script) ?? Environment.CurrentDirectory,
            ArgumentList = { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script }
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 PowerShell");
        process.WaitForExit();
        return process.ExitCode;
    }
}
 
