using System.Collections.Generic;

namespace ClassIsland.Services.VoiceControl.Models;

/// <summary>
/// 语音指令意图分类。
/// </summary>
public enum VoiceIntent
{
    Unknown = 0,

    // 桌面操作
    ShowDesktop,     // 回到/显示桌面（Win+D 同义词）
    MinimizeWindows, // 收起窗口

    // 软件启动
    LaunchApp,       // 打开/启动某软件

    // 系统电源（高危）
    RestartComputer, // 重启电脑
    ShutdownComputer,// 关机

    // 音量控制
    VolumeMute,      // 静音
    VolumeUnmute,    // 取消静音
    VolumeUp,        // 调大音量
    VolumeDown,      // 调小音量
    VolumeSet        // 音量设置到 [0-100]
}

/// <summary>
/// 解析后的语音指令。
/// </summary>
public sealed class VoiceCommand
{
    public VoiceIntent Intent { get; init; } = VoiceIntent.Unknown;

    /// <summary>原始识别文本。</summary>
    public string RawText { get; init; } = string.Empty;

    /// <summary>启动应用时的软件名（LaunchApp 用）。</summary>
    public string? AppName { get; init; }

    /// <summary>音量目标值 0-100（VolumeSet 用）。</summary>
    public int? VolumeLevel { get; init; }

    /// <summary>是否为高危指令，需要二次确认。</summary>
    public bool RequiresConfirmation =>
        Intent is VoiceIntent.RestartComputer or VoiceIntent.ShutdownComputer;

    /// <summary>高危操作的中文名称，用于确认提示。</summary>
    public string? DangerousActionName =>
        Intent switch
        {
            VoiceIntent.RestartComputer => "重启",
            VoiceIntent.ShutdownComputer => "关机",
            _ => null
        };
}

/// <summary>
/// 指令执行结果。
/// </summary>
public sealed class VoiceCommandResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public VoiceIntent Intent { get; init; }
}

/// <summary>
/// 软件名 -> 可执行文件/启动方式 的映射表（来自 JSON 配置）。
/// </summary>
public sealed class AppLaunchMap : Dictionary<string, string>
{
}
