using System;
using ClassIsland.Services.VoiceControl.Models;

namespace ClassIsland.Services.VoiceControl.Abstractions;

/// <summary>
/// 高危操作执行前的安全回调。返回 true 表示已通过确认，可执行。
/// 由编排器/UI 注入，用于实现"二次确认悬浮窗"防御。
/// </summary>
/// <param name="command">需要确认的高危指令。</param>
public delegate bool DangerousActionGuard(VoiceCommand command);

/// <summary>
/// 系统指令执行器接口。根据解析后的指令执行真实动作。
/// 对重启/关机等高危险指令，执行器本身应拒绝直接执行，必须由编排器经二次确认后调用对应方法。
/// </summary>
public interface ISystemExecutor : IDisposable
{
    /// <summary>返回桌面（模拟 Win+D）。</summary>
    void ShowDesktop();

    /// <summary>收起/最小化所有窗口。</summary>
    void MinimizeWindows();

    /// <summary>启动应用。优先从解析器映射表取路径，否则按系统别名/文件名尝试。</summary>
    /// <param name="appName">软件名或启动标识。</param>
    void LaunchApp(string appName);

    /// <summary>调大音量（步进）。</summary>
    void VolumeUp();

    /// <summary>调小音量（步进）。</summary>
    void VolumeDown();

    /// <summary>切换静音状态。</summary>
    void ToggleMute();

    /// <summary>取消静音（确保有声音）。</summary>
    void Unmute();

    /// <summary>设置音量到 0-100。</summary>
    void SetVolume(int level);

    /// <summary>
    /// 重启电脑。仅应在二次确认通过后调用。
    /// 内部带有安全令牌防御：若未通过确认，记录警告并拒绝执行。
    /// </summary>
    /// <param name="guardPassed">二次确认是否已通过（默认 false）。</param>
    void RestartComputer(bool guardPassed = false);

    /// <summary>
    /// 关机。仅应在二次确认通过后调用。
    /// </summary>
    /// <param name="guardPassed">二次确认是否已通过（默认 false）。</param>
    void ShutdownComputer(bool guardPassed = false);

    /// <summary>统一执行入口（含高危拦截逻辑）。</summary>
    /// <param name="command">已解析指令。</param>
    /// <param name="isDangerousAllowed">高危指令是否已被确认允许。</param>
    /// <returns>执行结果。</returns>
    VoiceCommandResult Execute(VoiceCommand command, bool isDangerousAllowed = false);
}
