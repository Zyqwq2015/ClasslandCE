using System;
using System.Threading;
using ClassIsland.Services.VoiceControl.Abstractions;
using ClassIsland.Services.VoiceControl.Models;

namespace ClassIsland.Services.VoiceControl.Core;

/// <summary>
/// 语音控制编排器：串联 唤醒引擎 → 命令解析 → 系统执行 → UI 状态，
/// 并集中处理高危指令的二次确认流程。
/// 所有模块通过接口注入，编排器本身不依赖具体实现，便于替换/测试。
/// </summary>
public sealed class VoiceControlOrchestrator : IDisposable
{
    private readonly IVoiceWakeEngine _engine;
    private readonly ICommandParser _parser;
    private readonly ISystemExecutor _executor;
    private readonly IVoiceStatusViewModel _viewModel;
    private readonly IVoiceLogger _logger;
    private readonly SynchronizationContext? _uiContext;
    private bool _disposed;

    /// <summary>
    /// 高危操作（重启/关机）确认回调。由宿主（WPF 主线程）注入：
    /// 弹出毛玻璃二次确认窗 + TTS 播报，并返回用户是否确认（true=确认）。
    /// 若未注入，高危操作默认拒绝执行。
    /// </summary>
    public Func<VoiceCommand, bool>? DangerousConfirmationCallback { get; set; }

    public VoiceControlOrchestrator(
        IVoiceWakeEngine engine,
        ICommandParser parser,
        ISystemExecutor executor,
        IVoiceStatusViewModel viewModel,
        IVoiceLogger logger,
        SynchronizationContext? uiContext = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _uiContext = uiContext;
    }

    /// <summary>初始化并依据设置决定是否开始监听。</summary>
    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VoiceControlOrchestrator));

        _engine.WakeWordDetected += OnWakeWordDetected;
        _engine.CommandRecognized += OnCommandRecognized;
        _engine.MicSilenceDetected += OnMicSilenceDetected;

        try { _engine.Initialize(); }
        catch (Exception ex) { _logger.Error("唤醒引擎初始化异常", ex); }

        _viewModel.SetState(VoiceControlState.Standby);
        if (_viewModel.IsEnabled)
            EnableListening();
    }

    /// <summary>启用/禁用语音监听（绑定设置面板开关）。</summary>
    public void SetEnabled(bool enabled)
    {
        _viewModel.IsEnabled = enabled;
        if (enabled) EnableListening();
        else DisableListening();
    }

    private void EnableListening()
    {
        try
        {
            if (!_engine.IsListening) _engine.StartListening();
            RunOnUi(() => _viewModel.SetState(VoiceControlState.Listening));
            _logger.Info("语音监听已启用。");
        }
        catch (Exception ex) { _logger.Error("启用监听失败", ex); }
    }

    private void DisableListening()
    {
        try
        {
            if (_engine.IsListening) _engine.StopListening();
            RunOnUi(() => _viewModel.SetState(VoiceControlState.Standby));
            _logger.Info("语音监听已禁用。");
        }
        catch (Exception ex) { _logger.Error("禁用监听失败", ex); }
    }

    private void OnWakeWordDetected(object? sender, WakeWordDetectedEventArgs e)
    {
        _logger.Info($"唤醒：{e.MatchedPhrase}，开始识别命令…");
        RunOnUi(() => _viewModel.SetState(VoiceControlState.Recognizing));
    }

    private void OnCommandRecognized(object? sender, CommandRecognizedEventArgs e)
    {
        try { HandleCommand(e.Text, e.Success); }
        catch (Exception ex) { _logger.Error("处理命令异常", ex); }
    }

    /// <summary>监听启动后无麦克风输入：把诊断信息写入设置页「运行状态」卡片。</summary>
    private void OnMicSilenceDetected(object? sender, EventArgs e)
        => _viewModel.NotifyStatus("未检测到麦克风输入：请检查系统默认录音设备是否可用、麦克风是否静音或被其他程序占用。");

    /// <summary>解析并执行命令；高危指令走二次确认。</summary>
    public void HandleCommand(string? text, bool success = true)
    {
        RunOnUi(() =>
        {
            _viewModel.NotifyRecognized(text ?? string.Empty);
            _viewModel.SetState(VoiceControlState.Executing);
        });

        if (!success || string.IsNullOrWhiteSpace(text))
        {
            _logger.Warning("未识别到有效命令文本。");
            RunOnUi(() =>
            {
                _viewModel.NotifyResult(new VoiceCommandResult { Success = false, Intent = VoiceIntent.Unknown, Message = "没有听清，请再说一次。" });
                _viewModel.SetState(VoiceControlState.Listening);
            });
            return;
        }

        var command = _parser.Parse(text);
        _logger.Info($"解析意图：{command.Intent}（原文：{text}）");

        if (command.Intent == VoiceIntent.Unknown)
        {
            RunOnUi(() =>
            {
                _viewModel.NotifyResult(new VoiceCommandResult { Success = false, Intent = VoiceIntent.Unknown, Message = "未识别的指令。" });
                _viewModel.SetState(VoiceControlState.Listening);
            });
            return;
        }

        bool allowed = false;
        if (command.RequiresConfirmation)
        {
            _logger.Warning($"检测到高危指令：{command.DangerousActionName}，进入二次确认。");
            try { allowed = DangerousConfirmationCallback?.Invoke(command) ?? false; }
            catch (Exception ex) { _logger.Error("二次确认回调异常，按拒绝处理", ex); allowed = false; }

            if (!allowed)
            {
                RunOnUi(() =>
                {
                    _viewModel.NotifyResult(new VoiceCommandResult { Success = false, Intent = command.Intent, Message = "用户未确认，已取消。" });
                    _viewModel.SetState(VoiceControlState.Listening);
                });
                return;
            }
        }

        var result = _executor.Execute(command, allowed);
        _logger.Info($"执行结果：{result.Success} - {result.Message}");

        RunOnUi(() =>
        {
            _viewModel.NotifyResult(result);
            _viewModel.SetState(VoiceControlState.Listening);
        });
    }

    private void RunOnUi(Action action)
    {
        if (_uiContext != null)
            _uiContext.Post(_ => action(), null);
        else
            action();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _engine.WakeWordDetected -= OnWakeWordDetected;
            _engine.CommandRecognized -= OnCommandRecognized;
            _engine.MicSilenceDetected -= OnMicSilenceDetected;
            if (_engine.IsListening) _engine.StopListening();
        }
        catch { /* ignore */ }
        try { _engine.Dispose(); } catch { /* ignore */ }
        try { _executor.Dispose(); } catch { /* ignore */ }
    }
}
