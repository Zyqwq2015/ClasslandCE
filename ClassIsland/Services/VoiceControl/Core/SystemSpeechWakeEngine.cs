using System;
using System.Linq;
using System.Speech.Recognition;
using System.Text.RegularExpressions;
using System.Timers;
using ClassIsland.Services.VoiceControl.Abstractions;

namespace ClassIsland.Services.VoiceControl.Core;

/// <summary>
/// 基于 System.Speech.Recognition (SAPI) 的离线语音唤醒引擎。
/// 待机时使用"唤醒词语法"，命中后切换为"听写语法"捕捉命令，静默超时后输出文本。
/// 不依赖网络，纯本地。生产环境若要更高唤醒率，可替换为 Porcupine / Vosk 实现同一 <see cref="IVoiceWakeEngine"/>。
/// </summary>
public sealed class SystemSpeechWakeEngine : VoiceWakeEngineBase
{
    private readonly IVoiceLogger _logger;
    private SpeechRecognitionEngine? _engine;
    private Grammar? _wakeGrammar;
    private Grammar? _commandGrammar;
    private bool _commandMode;
    private string? _lastCommandText;
    private Timer? _commandTimer;
    private bool _available;
    private string _recognitionLanguage = string.Empty;

    /// <summary>开始监听后是否检测到任何音频电平（&gt;0），用于静默诊断。</summary>
    private bool _audioDetected;

    /// <summary>启动监听后无音频输入的诊断定时器。</summary>
    private Timer? _micCheckTimer;

    /// <summary>监听启动后等待音频输入的超时（毫秒），超时仍无输入则触发 MicSilenceDetected。</summary>
    private const int MicSilenceCheckMs = 5000;

    public bool IsAvailable => _available;

    /// <summary>实际使用的 SAPI 识别器语言（如 zh-CN / en-US），用于诊断是否真的在用中文识别。</summary>
    public string RecognitionLanguage => _recognitionLanguage;

    public SystemSpeechWakeEngine(IVoiceLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Initialize()
    {
        try
        {
            var recognizers = SpeechRecognitionEngine.InstalledRecognizers();
            if (recognizers.Count == 0)
            {
                _logger.Error("未安装任何语音识别引擎（SAPI），请先在 Windows 中安装语音识别语言包。");
                return;
            }

            // 优先简体中文（zh-CN），其次任意中文，最后退回系统第一个识别器——避免选到繁体/英文
            // 识别器导致普通话唤醒词几乎无法命中。
            var info = recognizers.FirstOrDefault(r => r.Culture.Name.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
                       ?? recognizers.FirstOrDefault(r => r.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                       ?? recognizers[0];

            _recognitionLanguage = info.Culture.Name;
            _engine = new SpeechRecognitionEngine(info);
            _engine.SpeechRecognized += OnSpeechRecognized;
            _engine.RecognizeCompleted += OnRecognizeCompleted;
            _engine.AudioStateChanged += OnAudioStateChanged;
            _engine.AudioLevelUpdated += OnAudioLevelUpdated;

            // 远场友好调优：调高尾音静音超时与含糊结果超时，避免小音量/长停顿时
            // 识别器过早切词、把尾音截断导致识别失败。InitialSilenceTimeout/BabbleTimeout
            // 默认即"无限"，保持不动。
            try
            {
                _engine.EndSilenceTimeout = TimeSpan.FromMilliseconds(1000);
                _engine.EndSilenceTimeoutAmbiguous = TimeSpan.FromMilliseconds(1500);
            }
            catch (Exception ex)
            {
                _logger.Warning($"设置 SAPI 静音超时失败（使用默认值）：{ex.Message}");
            }

            _engine.SetInputToDefaultAudioDevice();

            BuildGrammars();
            if (_wakeGrammar != null)
                _engine.LoadGrammar(_wakeGrammar);

            _available = true;
            _logger.Info($"语音唤醒引擎初始化完成，识别语言：{info.Culture.Name}");
        }
        catch (Exception ex)
        {
            _available = false;
            _logger.Error("语音唤醒引擎初始化失败", ex);
        }
    }

    private void BuildGrammars()
    {
        if (_engine == null) return;

        // 待机语法：仅包含唤醒词及其同义词
        var wakeChoices = new Choices();
        wakeChoices.Add(WakeWord);
        foreach (var alias in WakeWordAliases)
            wakeChoices.Add(alias);
        _wakeGrammar = new Grammar(new GrammarBuilder(wakeChoices)) { Name = "wake" };

        // 命令语法：中文听写（v1 快速闭环）
        try
        {
            _commandGrammar = new DictationGrammar { Name = "command" };
        }
        catch (Exception ex)
        {
            _logger.Warning($"听写语法不可用，将退化为关键词识别：{ex.Message}");
            _commandGrammar = null;
        }
    }

    public override void StartListening()
    {
        if (_engine == null || !_available)
        {
            _logger.Error("引擎未就绪，无法开始监听。");
            return;
        }
        if (IsListening) return;

        try
        {
            EnsureWakeGrammar();
            _engine.RecognizeAsync(RecognizeMode.Multiple);
            IsListening = true;
            _audioDetected = false;
            StartMicCheckTimer();
            _logger.Info("语音唤醒监听已启动（待机语法）。");
        }
        catch (Exception ex)
        {
            _logger.Error("启动监听失败", ex);
        }
    }

    public override void StopListening()
    {
        if (!IsListening) return;
        try
        {
            _engine?.RecognizeAsyncStop();
        }
        catch (Exception ex)
        {
            _logger.Error("停止监听时异常", ex);
        }
        finally
        {
            _commandMode = false;
            StopCommandTimer();
            StopMicCheckTimer();
            IsListening = false;
            _logger.Info("语音唤醒监听已停止。");
        }
    }

    protected override void OnLowPowerChanged(bool enabled)
    {
        _logger.Info($"低功耗模式：{(enabled ? "开启" : "关闭")}");
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        var text = e.Result?.Text ?? string.Empty;
        var confidence = e.Result?.Confidence ?? 0d;

        if (_commandMode)
        {
            // 命令阶段：远场 / 小音量也不限置信度，只要能识别到文本就采纳，交给解析器兜底。
            _lastCommandText = text;
            _logger.Trace($"命令片段：{text} ({confidence:F2})");
            ResetCommandTimer();
            return;
        }

        // 待机阶段：唤醒词判定。
        // 远场场景下 SAPI 返回的信心值会显著下降，若沿用常规门槛（1 - Sensitivity）极易被误杀，
        // 因此改用更低的专用底线 MinWakeConfidence（默认 0.05，几乎只过滤纯噪声）。
        // 是否为唤醒词本身仍交由 IsWakeMatch（归一化子串匹配）判定，对轻微误识更宽容。
        if (confidence < MinWakeConfidence)
        {
            _logger.Trace($"低置信度丢弃（低于唤醒底线 {MinWakeConfidence:F2}）：{text} ({confidence:F2})");
            return;
        }

        if (IsWakeMatch(text))
        {
            _logger.Info($"唤醒词命中：{text} ({confidence:F2})");
            EnterCommandMode(text);
        }
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        if (e.Error != null)
            _logger.Warning($"识别会话异常：{e.Error.Message}");

        if (IsListening && !e.Cancelled)
        {
            try { _engine?.RecognizeAsync(RecognizeMode.Multiple); }
            catch (Exception ex) { _logger.Error("尝试重启识别会话失败", ex); }
        }
    }

    private void OnAudioStateChanged(object? sender, AudioStateChangedEventArgs e)
    {
        // 静默状态可用于更及时地结束命令采集，这里仅记录，主逻辑由静默计时器驱动。
        if (e.AudioState == AudioState.Silence && _commandMode)
            _logger.Trace("检测到静默。");
    }

    private void OnAudioLevelUpdated(object? sender, AudioLevelUpdatedEventArgs e)
    {
        if (e.AudioLevel > 0)
        {
            _audioDetected = true;
            _logger.Trace($"音频电平：{e.AudioLevel}");
        }
    }

    /// <summary>
    /// 监听启动后 5 秒内无任何音频电平 → 判定麦克风链路不通，
    /// 记录日志并触发 MicSilenceDetected（设置页「运行状态」卡片会提示）。
    /// </summary>
    private void StartMicCheckTimer()
    {
        StopMicCheckTimer();
        _micCheckTimer = new Timer(MicSilenceCheckMs) { AutoReset = false };
        _micCheckTimer.Elapsed += (_, _) =>
        {
            if (_audioDetected) return;
            _logger.Warning("启动监听后未检测到麦克风输入：请检查系统默认录音设备是否可用、麦克风是否静音或被其他程序占用。");
            RaiseMicSilence();
        };
        _micCheckTimer.Start();
    }

    private void StopMicCheckTimer()
    {
        if (_micCheckTimer == null) return;
        try { _micCheckTimer.Stop(); _micCheckTimer.Dispose(); } catch { /* ignore */ }
        _micCheckTimer = null;
    }

    private static string Normalize(string s) =>
        Regex.Replace(s, @"[\s\p{P}]+", string.Empty);

    private bool IsWakeMatch(string text)
    {
        var norm = Normalize(text);
        if (norm.Contains(Normalize(WakeWord), StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (var alias in WakeWordAliases)
            if (norm.Contains(Normalize(alias), StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void EnterCommandMode(string matchedPhrase)
    {
        _commandMode = true;
        _lastCommandText = null;
        try
        {
            if (_wakeGrammar != null) _engine!.UnloadGrammar(_wakeGrammar);
            if (_commandGrammar != null) _engine!.LoadGrammar(_commandGrammar);
        }
        catch (Exception ex)
        {
            _logger.Warning($"切换命令语法失败：{ex.Message}");
        }
        StartCommandTimer();
        RaiseWakeWord(matchedPhrase);
    }

    private void StartCommandTimer()
    {
        StopCommandTimer();
        var due = Math.Max(1.0, SilenceTimeoutSeconds * (LowPowerMode ? 1.5 : 1.0)) * 1000.0;
        _commandTimer = new Timer(due) { AutoReset = false };
        _commandTimer.Elapsed += (_, _) => FinalizeCommandMode();
        _commandTimer.Start();
    }

    private void ResetCommandTimer()
    {
        if (_commandTimer == null) return;
        try { _commandTimer.Stop(); _commandTimer.Start(); } catch { /* ignore */ }
    }

    private void StopCommandTimer()
    {
        if (_commandTimer == null) return;
        try { _commandTimer.Stop(); _commandTimer.Dispose(); } catch { /* ignore */ }
        _commandTimer = null;
    }

    private void FinalizeCommandMode()
    {
        if (!_commandMode) return;
        _commandMode = false;
        StopCommandTimer();

        try
        {
            if (_commandGrammar != null) _engine?.UnloadGrammar(_commandGrammar);
            EnsureWakeGrammar();
        }
        catch (Exception ex)
        {
            _logger.Warning($"恢复待机语法失败：{ex.Message}");
        }

        var text = _lastCommandText;
        _logger.Info($"命令识别结束：{text ?? "(空)"}");
        RaiseCommand(text, !string.IsNullOrWhiteSpace(text));
    }

    private void EnsureWakeGrammar()
    {
        if (_engine == null || _wakeGrammar == null) return;
        try
        {
            var loaded = _engine.Grammars.Any(g => g.Name == "wake");
            if (!loaded) _engine.LoadGrammar(_wakeGrammar);
        }
        catch { /* 可能已加载，忽略 */ }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopCommandTimer();
            StopMicCheckTimer();
            try { _engine?.Dispose(); } catch { /* ignore */ }
            _engine = null;
        }
        base.Dispose(disposing);
    }
}
