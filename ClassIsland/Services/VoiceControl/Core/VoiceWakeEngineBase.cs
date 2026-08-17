using System;
using System.Collections.Generic;
using System.Threading;
using ClassIsland.Services.VoiceControl.Abstractions;

namespace ClassIsland.Services.VoiceControl.Core;

/// <summary>
/// 语音唤醒引擎的抽象基类，封装通用状态、事件与低功耗节流逻辑。
/// 具体实现（System.Speech / Porcupine / Vosk）只需关注音频采集与唤醒检测。
/// </summary>
public abstract class VoiceWakeEngineBase : IVoiceWakeEngine
{
    private bool _isListening;
    private bool _lowPowerMode;
    private double _sensitivity = 0.6;
    private int _silenceTimeoutSeconds = 3;
    private readonly object _stateLock = new();

    public string WakeWord { get; set; } = "小课小课";

    public IReadOnlyList<string> WakeWordAliases { get; set; } = Array.Empty<string>();

    public bool IsListening
    {
        get { lock (_stateLock) return _isListening; }
        protected set { lock (_stateLock) { _isListening = value; } }
    }

    public bool LowPowerMode
    {
        get => _lowPowerMode;
        set
        {
            _lowPowerMode = value;
            OnLowPowerChanged(value);
        }
    }

    public double Sensitivity
    {
        get => _sensitivity;
        set => _sensitivity = Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>
    /// 唤醒词判定的最低置信度底线（独立于 Sensitivity）。
    /// 远场 / 小音量场景下 SAPI 返回的信心值会显著下降，若用常规门槛会直接丢弃唤醒词，
    /// 因此唤醒阶段使用更低的专用底线，确保"离远一点也能唤醒"。
    /// 取值 0.0 ~ 0.6，默认 0.05（极低，几乎只过滤纯噪声）。
    /// </summary>
    private double _minWakeConfidence = 0.05;
    public double MinWakeConfidence
    {
        get => _minWakeConfidence;
        set => _minWakeConfidence = Math.Clamp(value, 0.0, 0.6);
    }

    public int SilenceTimeoutSeconds
    {
        get => _silenceTimeoutSeconds;
        set => _silenceTimeoutSeconds = Math.Clamp(value, 1, 10);
    }

    public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;
    public event EventHandler<CommandRecognizedEventArgs>? CommandRecognized;
    public event EventHandler? MicSilenceDetected;

    /// <summary>低功耗模式下，识别轮询间隔放大系数（降低 CPU 占用）。</summary>
    protected int LowPowerPollDelayMs => LowPowerMode ? 250 : 40;

    public abstract void Initialize();
    public abstract void StartListening();
    public abstract void StopListening();

    /// <summary>低功耗状态变更钩子，供具体实现调整识别频率。</summary>
    protected virtual void OnLowPowerChanged(bool enabled) { }

    protected void RaiseWakeWord(string phrase)
    {
        try { WakeWordDetected?.Invoke(this, new WakeWordDetectedEventArgs { MatchedPhrase = phrase }); }
        catch (Exception ex) { OnRaiseError("WakeWordDetected", ex); }
    }

    protected void RaiseCommand(string? text, bool success)
    {
        try { CommandRecognized?.Invoke(this, new CommandRecognizedEventArgs { Text = text, Success = success }); }
        catch (Exception ex) { OnRaiseError("CommandRecognized", ex); }
    }

    /// <summary>触发"麦克风无输入"诊断事件（监听启动后静默超时）。</summary>
    protected void RaiseMicSilence()
    {
        try { MicSilenceDetected?.Invoke(this, EventArgs.Empty); }
        catch (Exception ex) { OnRaiseError("MicSilenceDetected", ex); }
    }

    /// <summary>事件订阅者抛出异常时的兜底（避免一个坏订阅者拖垮引擎）。</summary>
    protected virtual void OnRaiseError(string eventName, Exception ex)
    {
        // 默认空实现；具体引擎可注入日志。
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { if (IsListening) StopListening(); } catch { /* ignore */ }
        }
    }
}
