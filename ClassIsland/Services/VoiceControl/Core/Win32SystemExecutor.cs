using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using ClassIsland.Services.VoiceControl.Abstractions;
using ClassIsland.Services.VoiceControl.Core;
using ClassIsland.Services.VoiceControl.Models;

namespace ClassIsland.Services.VoiceControl.Core;

/// <summary>
/// 系统指令执行器：通过 Win32 API（user32 / shell32）与 Core Audio 完成真实动作。
/// 对重启/关机等高危指令，未经二次确认（guardPassed=false）一律拒绝执行，仅记录日志。
/// </summary>
public sealed class Win32SystemExecutor : ISystemExecutor, IDisposable
{
    private readonly IVoiceLogger _logger;
    private readonly CoreAudioVolumeController _volume;

    // user32：模拟键盘
    private const byte VK_LWIN = 0x5B;
    private const byte VK_KEY_D = 0x44;
    private const byte VK_KEY_M = 0x4D;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    // shell32：以"打开"动词启动文件/URL/应用（等价于双击）
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ShellExecuteW")]
    private static extern IntPtr ShellExecute(IntPtr hwnd, string lpOperation, string lpFile,
        string? lpParameters, string? lpDirectory, int nShowCmd);

    private const int SW_SHOWNORMAL = 1;

    // 系统自带软件别名
    private static readonly Dictionary<string, string> SystemAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["记事本"] = "notepad", ["notepad"] = "notepad",
        ["计算器"] = "calc", ["calc"] = "calc",
        ["命令提示符"] = "cmd", ["cmd"] = "cmd",
        ["画图"] = "mspaint", ["浏览器"] = "https://www.bing.com",
        ["默认浏览器"] = "https://www.bing.com",
    };

    public Win32SystemExecutor(IVoiceLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _volume = new CoreAudioVolumeController(logger);
    }

    public void ShowDesktop()
    {
        try
        {
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_KEY_D, 0, 0, UIntPtr.Zero);
            keybd_event(VK_KEY_D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            _logger.Info("已发送 显示桌面 (Win+D)。");
        }
        catch (Exception ex) { _logger.Error("显示桌面失败", ex); }
    }

    public void MinimizeWindows()
    {
        try
        {
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_KEY_M, 0, 0, UIntPtr.Zero);
            keybd_event(VK_KEY_M, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            _logger.Info("已发送 收起窗口 (Win+M)。");
        }
        catch (Exception ex) { _logger.Error("收起窗口失败", ex); }
    }

    public void LaunchApp(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return;

        var target = ResolveLaunchTarget(appName);
        _logger.Info($"启动应用：{appName} -> {target}");

        try
        {
            var hInst = ShellExecute(IntPtr.Zero, "open", target, null, null, SW_SHOWNORMAL);
            // 返回值 > 32 表示成功
            if ((long)hInst > 32)
            {
                _logger.Info($"已通过 ShellExecute 启动：{target}");
                return;
            }
            _logger.Warning($"ShellExecute 返回 {(long)hInst}，回退至 Process.Start。");
        }
        catch (Exception ex)
        {
            _logger.Warning($"ShellExecute 失败，回退至 Process.Start：{ex.Message}");
        }

        // 回退：.NET Process（UseShellExecute 内部同样走 shell32）
        try
        {
            var psi = new ProcessStartInfo(target) { UseShellExecute = true };
            Process.Start(psi);
            _logger.Info($"已通过 Process.Start 启动：{target}");
        }
        catch (Exception ex)
        {
            _logger.Error($"无法启动应用：{target}", ex);
        }
    }

    private static string ResolveLaunchTarget(string appName)
    {
        if (SystemAliases.TryGetValue(appName, out var alias) && !string.IsNullOrWhiteSpace(alias))
            return alias;
        return appName;
    }

    public void VolumeUp() => _volume.VolumeUp();
    public void VolumeDown() => _volume.VolumeDown();
    public void ToggleMute() => _volume.ToggleMute();
    public void Unmute() => _volume.Unmute();
    public void SetVolume(int level) => _volume.SetVolume(level);

    public void RestartComputer(bool guardPassed = false)
    {
        if (!guardPassed)
        {
            _logger.Warning("拦截重启指令：未通过二次确认，已拒绝执行（安全防御）。");
            return;
        }
        try
        {
            RunShutdown("/r /t 5");
            _logger.Info("已发起重启（5 秒后）。");
        }
        catch (Exception ex) { _logger.Error("重启失败", ex); }
    }

    public void ShutdownComputer(bool guardPassed = false)
    {
        if (!guardPassed)
        {
            _logger.Warning("拦截关机指令：未通过二次确认，已拒绝执行（安全防御）。");
            return;
        }
        try
        {
            RunShutdown("/s /t 5");
            _logger.Info("已发起关机（5 秒后）。");
        }
        catch (Exception ex) { _logger.Error("关机失败", ex); }
    }

    private static void RunShutdown(string arguments)
    {
        var psi = new ProcessStartInfo("shutdown", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(psi);
    }

    public VoiceCommandResult Execute(VoiceCommand command, bool isDangerousAllowed = false)
    {
        try
        {
            switch (command.Intent)
            {
                case VoiceIntent.ShowDesktop: ShowDesktop(); break;
                case VoiceIntent.MinimizeWindows: MinimizeWindows(); break;
                case VoiceIntent.LaunchApp: LaunchApp(command.AppName ?? string.Empty); break;
                case VoiceIntent.VolumeMute: ToggleMute(); break;
                case VoiceIntent.VolumeUnmute: Unmute(); break;
                case VoiceIntent.VolumeUp: VolumeUp(); break;
                case VoiceIntent.VolumeDown: VolumeDown(); break;
                case VoiceIntent.VolumeSet: SetVolume(command.VolumeLevel ?? 50); break;
                case VoiceIntent.RestartComputer:
                    RestartComputer(isDangerousAllowed);
                    if (!isDangerousAllowed)
                        return new VoiceCommandResult { Success = false, Intent = command.Intent, Message = "高危操作未确认，已拒绝。" };
                    break;
                case VoiceIntent.ShutdownComputer:
                    ShutdownComputer(isDangerousAllowed);
                    if (!isDangerousAllowed)
                        return new VoiceCommandResult { Success = false, Intent = command.Intent, Message = "高危操作未确认，已拒绝。" };
                    break;
                default:
                    return new VoiceCommandResult { Success = false, Intent = command.Intent, Message = "未识别的指令。" };
            }
            return new VoiceCommandResult { Success = true, Intent = command.Intent, Message = "执行成功。" };
        }
        catch (Exception ex)
        {
            _logger.Error($"执行指令失败：{command.Intent}", ex);
            return new VoiceCommandResult { Success = false, Intent = command.Intent, Message = $"执行异常：{ex.Message}" };
        }
    }

    public void Dispose() => _volume.Dispose();
}
