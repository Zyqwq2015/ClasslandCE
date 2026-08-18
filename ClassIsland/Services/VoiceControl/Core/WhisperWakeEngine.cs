using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.Services.VoiceControl.Abstractions;
using NAudio.Wave;
using Whisper.net;

namespace ClassIsland.Services.VoiceControl.Core;

/// <summary>
/// 基于 VAD（语音活动检测）+ Whisper.net 的离线语音唤醒引擎。
/// 不依赖 Windows 语音识别 / 在线服务：常驻一个极低耗的能量 VAD 检测"是否有人在说话"，
/// 检测到语音段后用本地 Whisper 模型做中文语音转写，再按文本匹配唤醒词进入指令模式。
/// 唤醒词无需训练——只要 Whisper 能把你说的话转写成文字、其中包含唤醒词即可触发，
/// 因此「小课小课」这类中文词开箱即用。
/// </summary>
public sealed class WhisperWakeEngine : VoiceWakeEngineBase
{
    private readonly IVoiceLogger _logger;
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;

    private WaveInEvent? _waveIn;
    private bool _available;
    private string _modelPath = string.Empty;
    private string _modelName = string.Empty;

    // ---- VAD 状态（仅在音频回调线程内访问）----
    private readonly List<float> _segmentBuffer = new();
    private bool _speaking;
    private int _speechCounter;
    private int _silenceCounter;
    private double _noiseFloor = 0.012;
    private bool _anyAudio;

    // ---- 指令模式 / 推理队列 ----
    private readonly ConcurrentQueue<float[]> _pendingSegments = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _inferenceTask;
    private bool _awaitingCommand;
    private DateTime _commandModeEnter = DateTime.MinValue;

    // ---- 麦克风静默诊断 ----
    private DateTime _listenStart = DateTime.MinValue;
    private Timer? _micCheckTimer;

    private const int SampleRate = 16000;
    private const int SpeechOnSamples = 1600;     // ~100ms 持续有声才判定为"开始说话"
    private const int SilenceOffSamples = 8000;    // ~500ms 持续无声才判定"这句话结束"
    private const int MaxSegmentSamples = SampleRate * 20; // 单段最多 20s，防异常长段

    public bool IsAvailable => _available;
    public string EngineInfo => _available ? $"Whisper 离线识别 · {_modelName}" : "Whisper 不可用";
    public string ModelPath { get => _modelPath; set => _modelPath = value; }

    /// <summary>默认下载的模型文件名（中文推荐 small 及以上；q5_0 为量化版，体积与精度折中）。</summary>
    public const string DefaultModelFileName = "ggml-small-q5_0.bin";

    /// <summary>模型准备过程中的状态广播（如缺失提示），供 UI 显示。</summary>
    public event Action<string>? ModelPreparationStatus;

    public WhisperWakeEngine(IVoiceLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>解析最终模型路径：若未显式设置，则取「程序目录/Models/ggml-model.bin」。</summary>
    private string ResolveModelPath()
    {
        if (!string.IsNullOrWhiteSpace(_modelPath))
            return _modelPath;
        return Path.Combine(AppContext.BaseDirectory, "Models", "ggml-model.bin");
    }

    public override void Initialize()
    {
        // 幂等：模型已加载过则跳过（例如先 EnsureModel 再被编排器调用）。
        if (_processor != null) return;

        _modelPath = ResolveModelPath();
        LoadModel();
    }

    private void LoadModel()
    {
        try
        {
            if (!File.Exists(_modelPath))
            {
                _available = false;
                _logger.Error(
                    $"未找到 Whisper 模型文件：{_modelPath}。" +
                    "请将 ggml 模型（如 ggml-small.bin / ggml-small-q5_0.bin）重命名为 ggml-model.bin 放入该路径，" +
                    "或确认发布包已携带内置模型，中文推荐使用 small 及以上规模（详见 Assets/VoiceWake/README.txt）。");
                return;
            }

            _modelName = Path.GetFileName(_modelPath);
            // 加载一次模型（较重），之后复用同一个 processor 做多段推理。
            _factory = WhisperFactory.FromPath(_modelPath);
            _processor = _factory.CreateBuilder()
                .WithLanguage("zh")
                .WithThreads(Math.Max(1, Environment.ProcessorCount))
                .Build();

            _available = true;
            _logger.Info($"Whisper 模型已加载：{_modelName}");
        }
        catch (Exception ex)
        {
            _available = false;
            _logger.Error("Whisper 初始化失败", ex);
        }
    }

    /// <summary>
    /// 确保模型就绪：本地已有则直接加载；缺失则返回 false，由调用方提示用户放入模型。
    /// 发布包已内置模型（&lt;程序目录&gt;/Models/ggml-model.bin），不再运行时联网下载。
    /// 详见 Assets/VoiceWake/README.txt。
    /// </summary>
    public bool EnsureModel()
    {
        _modelPath = ResolveModelPath();

        if (File.Exists(_modelPath))
        {
            LoadModel();
            return _available;
        }

        _available = false;
        ModelPreparationStatus?.Invoke(
            $"未找到 Whisper 模型：{_modelPath}。" +
            "请将 ggml 模型（如 ggml-small-q5_0.bin）重命名为 ggml-model.bin 放入该目录后重启应用。");
        _logger.Error(
            $"未找到 Whisper 模型文件：{_modelPath}。" +
            "发布包应已内置该模型；如缺失，请将 ggml 模型重命名为 ggml-model.bin 放入此目录（见 Assets/VoiceWake/README.txt）。");
        return false;
    }

    public override void StartListening()
    {
        if (!_available || _processor == null)
        {
            _logger.Error("Whisper 引擎未就绪，无法开始监听。");
            return;
        }
        if (IsListening) return;

        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = 50
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += (_, _) => { };

            _segmentBuffer.Clear();
            _speaking = false;
            _speechCounter = 0;
            _silenceCounter = 0;
            _anyAudio = false;
            _noiseFloor = 0.012;

            _inferenceTask = Task.Run(() => InferenceLoop(_cts.Token));

            _waveIn.StartRecording();
            IsListening = true;

            _listenStart = DateTime.Now;
            StartMicCheckTimer();

            _logger.Info("语音唤醒监听已启动（VAD + Whisper 离线识别）。");
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
            StopMicCheckTimer();
            try { _waveIn?.StopRecording(); } catch { /* ignore */ }
            try { _waveIn?.Dispose(); } catch { /* ignore */ }
            _waveIn = null;

            try { _cts.Cancel(); } catch { /* ignore */ }
        }
        finally
        {
            IsListening = false;
            _logger.Info("语音唤醒监听已停止。");
        }
    }

    /// <summary>音频回调：把 16-bit PCM 转 float，做能量 VAD 并切分出语音段入队。</summary>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            var bytes = e.BytesRecorded;
            if (bytes < 2) return;
            var samples = bytes / 2;
            for (var i = 0; i < samples; i++)
            {
                var s = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
                ProcessSample(s);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("音频回调处理异常", ex);
        }
    }

    private void ProcessSample(float s)
    {
        var abs = Math.Abs(s);
        if (abs > 0.005) _anyAudio = true;

        // 环境噪声地板（仅在"未说话"时缓慢更新）
        if (!_speaking)
            _noiseFloor = _noiseFloor * 0.95 + abs * 0.05;

        // 当前阈值随灵敏度变化：灵敏度越高阈值越低、越容易判定为语音
        var multiplier = 2.0 + (1.0 - Sensitivity) * 4.0;
        var threshold = Math.Max(_noiseFloor * multiplier, 0.008);

        if (abs > threshold)
        {
            _speechCounter++;
            _silenceCounter = 0;
        }
        else
        {
            _silenceCounter++;
            if (_speaking) _speechCounter = 0;
        }

        if (!_speaking)
        {
            if (_speechCounter >= SpeechOnSamples)
            {
                _speaking = true;
                _speechCounter = 0;
                _segmentBuffer.Clear();
                _segmentBuffer.Add(s);
            }
        }
        else
        {
            _segmentBuffer.Add(s);
            if (_silenceCounter >= SilenceOffSamples || _segmentBuffer.Count >= MaxSegmentSamples)
            {
                // 一句话结束，把整段交给推理线程
                var seg = _segmentBuffer.ToArray();
                _segmentBuffer.Clear();
                _speaking = false;
                _silenceCounter = 0;
                _pendingSegments.Enqueue(seg);
            }
        }
    }

    /// <summary>后台推理循环：逐段跑 Whisper，并处理唤醒词 / 指令匹配。</summary>
    private async Task InferenceLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 指令模式超时：等了太久没等到指令，悄悄回到待机
                if (_awaitingCommand &&
                    (DateTime.Now - _commandModeEnter).TotalSeconds > Math.Max(1, SilenceTimeoutSeconds))
                {
                    _awaitingCommand = false;
                }

                if (_pendingSegments.TryDequeue(out var seg))
                {
                    await RunAsr(seg, ct);
                }
                else
                {
                    await Task.Delay(20, ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.Error("推理循环异常", ex);
            }
        }
    }

    private async Task RunAsr(float[] samples, CancellationToken ct)
    {
        if (_processor == null) return;
        var sb = new StringBuilder();
        await foreach (var segment in _processor.ProcessAsync(samples, ct))
            sb.Append(segment.Text);

        var text = sb.ToString().Trim();
        _logger.Info($"识别文本：{text}");
        if (string.IsNullOrWhiteSpace(text)) return;

        HandleTranscript(text);
    }

    private void HandleTranscript(string text)
    {
        var norm = Normalize(text);
        var wakeNorm = Normalize(WakeWord);

        if (!_awaitingCommand)
        {
            var matched = norm.Contains(wakeNorm, StringComparison.OrdinalIgnoreCase) ||
                          WakeWordAliases.Any(a => norm.Contains(Normalize(a), StringComparison.OrdinalIgnoreCase));
            if (!matched) return;

            RaiseWakeWord(WakeWord);
            _awaitingCommand = true;
            _commandModeEnter = DateTime.Now;

            // 同一句话里唤醒词后面可能直接跟着指令，例如"小课小课 打开记事本"
            var remainder = StripAfterWake(text);
            if (!string.IsNullOrWhiteSpace(remainder))
            {
                _awaitingCommand = false;
                RaiseCommand(remainder, true);
            }
        }
        else
        {
            _awaitingCommand = false;
            RaiseCommand(text, true);
        }
    }

    private static string Normalize(string s) =>
        Regex.Replace(s, @"[\s\p{P}]+", string.Empty).ToLower(CultureInfo.InvariantCulture);

    private string StripAfterWake(string text)
    {
        var idx = text.IndexOf(WakeWord, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            foreach (var alias in WakeWordAliases)
            {
                idx = text.IndexOf(alias, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) break;
            }
        }
        if (idx < 0) return text;
        var rest = text.Substring(idx + WakeWord.Length).Trim();
        return rest;
    }

    private void StartMicCheckTimer()
    {
        StopMicCheckTimer();
        _micCheckTimer = new Timer(_ =>
        {
            if (DateTime.Now - _listenStart < TimeSpan.FromSeconds(5)) return;
            if (_anyAudio) return;
            _logger.Warning("启动监听后未检测到麦克风输入：请检查系统默认录音设备是否可用、麦克风是否静音或被其他程序占用。");
            RaiseMicSilence();
        }, null, 5000, 2000);
    }

    private void StopMicCheckTimer()
    {
        try { _micCheckTimer?.Dispose(); } catch { /* ignore */ }
        _micCheckTimer = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopMicCheckTimer();
            try { _cts.Cancel(); } catch { /* ignore */ }
            try { _inferenceTask?.Wait(2000); } catch { /* ignore */ }
            try { _waveIn?.Dispose(); } catch { /* ignore */ }
            try { _processor?.Dispose(); } catch { /* ignore */ }
            try { _factory?.Dispose(); } catch { /* ignore */ }
            try { _cts.Dispose(); } catch { /* ignore */ }
        }
        base.Dispose(disposing);
    }
}
