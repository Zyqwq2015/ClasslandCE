using System;
using ClassIsland.Services.VoiceControl.Abstractions;

namespace ClassIsland.Services.VoiceControl.Abstractions;

/// <summary>
/// 唤醒词被检测到时的事件参数。
/// </summary>
public sealed class WakeWordDetectedEventArgs : EventArgs
{
    /// <summary>实际命中的唤醒词（可能来自同义词）。</summary>
    public string MatchedPhrase { get; init; } = string.Empty;
}

/// <summary>
/// 命令语音识别完成后的事件参数。
/// </summary>
public sealed class CommandRecognizedEventArgs : EventArgs
{
    /// <summary>纯文本识别结果（可能为空，表示识别失败/静默）。</summary>
    public string? Text { get; init; }

    /// <summary>是否成功识别到有效文本。</summary>
    public bool Success { get; init; }
}

/// <summary>
/// 语音唤醒引擎接口。
/// 负责初始化麦克风、在后台线程监听唤醒词；唤醒后开启语音接收，
/// 静默若干秒后输出识别文本。模块内所有唤醒实现（System.Speech / Porcupine / Vosk）均需实现此接口。
/// </summary>
public interface IVoiceWakeEngine : IDisposable
{
    /// <summary>当前唤醒词（可被设置面板自定义）。</summary>
    string WakeWord { get; set; }

    /// <summary>唤醒词同义词列表（例如"小课"）。</summary>
    System.Collections.Generic.IReadOnlyList<string> WakeWordAliases { get; set; }

    /// <summary>是否正在监听。</summary>
    bool IsListening { get; }

    /// <summary>
    /// 低功耗运行开关。开启后降低轮询/识别频率、拉长静默判定，以减少 CPU/电量占用。
    /// </summary>
    bool LowPowerMode { get; set; }

    /// <summary>麦克风灵敏度（0.0 - 1.0），影响识别置信度阈值。</summary>
    double Sensitivity { get; set; }

    /// <summary>唤醒后等待静默的时长（秒），默认 3。</summary>
    int SilenceTimeoutSeconds { get; set; }

    /// <summary>初始化麦克风与识别引擎。</summary>
    void Initialize();

    /// <summary>开始后台监听（待机语法）。</summary>
    void StartListening();

    /// <summary>停止监听并释放音频资源（保留实例可再次 StartListening）。</summary>
    void StopListening();

    /// <summary>检测到唤醒词时触发。</summary>
    event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

    /// <summary>命令语音识别完成时触发（唤醒后的识别阶段）。</summary>
    event EventHandler<CommandRecognizedEventArgs>? CommandRecognized;

    /// <summary>开始监听后一段时间内未检测到任何麦克风输入时触发（用于诊断）。</summary>
    event EventHandler? MicSilenceDetected;
}
