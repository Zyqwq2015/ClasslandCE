namespace ClassIsland.Services.VoiceControl.Abstractions;

/// <summary>
/// 极简日志抽象，便于替换为 Serilog / NLog / Microsoft.Extensions.Logging。
/// 模块内部统一通过该接口记录，避免直接依赖具体日志框架。
/// </summary>
public interface IVoiceLogger
{
    void Trace(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message, System.Exception? ex = null);
}

/// <summary>
/// 语音控制整体状态（用于 UI 绑定）。
/// </summary>
public enum VoiceControlState
{
    /// <summary>待机（未监听）。</summary>
    Standby = 0,

    /// <summary>监听唤醒词中。</summary>
    Listening,

    /// <summary>已唤醒，正在识别命令。</summary>
    Recognizing,

    /// <summary>正在执行命令。</summary>
    Executing
}
