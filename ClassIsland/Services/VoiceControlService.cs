using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services.SpeechService;
using ClassIsland.Models;
using ClassIsland.Services.VoiceControl.Abstractions;
using ClassIsland.Services.VoiceControl.Core;
using ClassIsland.Services.VoiceControl.Models;
using ClassIsland.Services.VoiceControl.ViewModels;
using ClassIsland.Views;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services;

/// <summary>
/// Classland CE 语音唤醒控制系统服务。
/// 串联 唤醒引擎 → 命令解析 → 系统执行，并复用现有 VoiceAssistantService 处理课表查询类指令。
/// 高危指令（重启/关机）走毛玻璃二次确认窗，未经确认绝不执行。
/// </summary>
public class VoiceControlService : IHostedService
{
    private readonly SettingsService _settingsService;
    private readonly ISpeechService _speechService;
    private readonly VoiceAssistantService _voiceAssistant;
    private readonly ILogger<VoiceControlService> _logger;
    private readonly VoiceLogger _voiceLogger;
    private readonly VoiceStatusViewModel _statusViewModel;

    private WhisperWakeEngine? _engine;
    private RegexCommandParser? _parser;
    private Win32SystemExecutor? _executor;
    private VoiceControlOrchestrator? _orchestrator;

    /// <summary>供麦克风状态指示器绑定的状态视图模型。</summary>
    public VoiceStatusViewModel StatusViewModel => _statusViewModel;

    public VoiceControlService(
        SettingsService settingsService,
        ISpeechService speechService,
        VoiceAssistantService voiceAssistant,
        ILogger<VoiceControlService> logger)
    {
        _settingsService = settingsService;
        _speechService = speechService;
        _voiceAssistant = voiceAssistant;
        _logger = logger;
        _voiceLogger = new VoiceLogger();
        _statusViewModel = new VoiceStatusViewModel();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            _statusViewModel.StatusMessage = "语音控制仅支持 Windows 平台，已跳过";
            _logger.LogWarning("[VoiceControl] 语音控制仅支持 Windows 平台，已跳过初始化。");
            return Task.CompletedTask;
        }
        if (!_settingsService.Settings.CeIsVoiceControlEnabled)
        {
            _statusViewModel.StatusMessage = "未启用：请在下方开启开关，重启应用后生效";
            _logger.LogInformation("[VoiceControl] 语音控制未启用（可在设置中开启）。");
            return Task.CompletedTask;
        }
        // 模型可能需联网下载（一次性），放到后台执行，避免阻塞应用启动。
        _ = Task.Run(BuildAndStartAsync, cancellationToken);
        return Task.CompletedTask;
    }

    private async Task BuildAndStartAsync()
    {
        try
        {
            var s = _settingsService.Settings;
            _engine = new WhisperWakeEngine(_voiceLogger)
            {
                WakeWord = string.IsNullOrWhiteSpace(s.CeVoiceWakeWord) ? "小课小课" : s.CeVoiceWakeWord,
                Sensitivity = s.CeVoiceSensitivity,
                MinWakeConfidence = s.CeVoiceMinWakeConfidence,
                LowPowerMode = s.CeVoiceLowPowerMode,
                SilenceTimeoutSeconds = 3,
                ModelPath = string.IsNullOrWhiteSpace(s.CeVoiceWhisperModelPath)
                    ? string.Empty
                    : s.CeVoiceWhisperModelPath
            };
            // 模型准备（缺失则自动下载）进度广播到状态栏
            _engine.ModelPreparationStatus += msg => _statusViewModel.StatusMessage = msg;

            _statusViewModel.IsEnabled = true;
            _statusViewModel.StatusMessage = "正在准备语音模型…";

            // 先确保模型就绪（自动下载），再启动监听
            var modelReady = await _engine.EnsureModelAsync(CancellationToken.None);
            if (!modelReady)
            {
                _statusViewModel.StatusMessage =
                    "Whisper 模型未就绪：自动下载失败，请手动下载模型后放入 Models 目录（详见日志）。";
                _logger.LogWarning("[VoiceControl] 模型未就绪，语音控制不可用。");
                return;
            }

            _parser = new RegexCommandParser();
            _parser.LoadAppLaunchMap(DefaultAppLaunchMap());
            _executor = new Win32SystemExecutor(_voiceLogger);

            _orchestrator = new VoiceControlOrchestrator(
                _engine, _parser, _executor, _statusViewModel, _voiceLogger, new AvaloniaSynchronizationContext())
            {
                DangerousConfirmationCallback = OnDangerousConfirmation
            };

            // 课表类指令（下一节课/今天课表等）未命中系统控制时，委托给现有语音助手
            _engine.CommandRecognized += OnCommandRecognizedFallback;

            _orchestrator.Start();
            _statusViewModel.StatusMessage = _engine.IsAvailable
                ? $"监听中 · 唤醒词「{_engine.WakeWord}」· {_engine.EngineInfo}"
                : "Whisper 模型未就绪：请下载 ggml 模型放入 Models/ 目录（详见日志与设置页提示）";
            _logger.LogInformation("[VoiceControl] 语音控制已启动，唤醒词：{Wake}", _engine.WakeWord);
        }
        catch (Exception ex)
        {
            _statusViewModel.StatusMessage = $"启动失败：{ex.Message}";
            _logger.LogError(ex, "[VoiceControl] 启动失败");
        }
    }

    private void OnCommandRecognizedFallback(object? sender, CommandRecognizedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Text)) return;
        try
        {
            var cmd = _parser!.Parse(e.Text);
            if (cmd.Intent == VoiceIntent.Unknown)
            {
                _voiceAssistant.ExecuteCommand(e.Text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VoiceControl] 课表指令 fallback 失败");
        }
    }

    /// <summary>
    /// 高危指令（重启/关机）二次确认：在 UI 线程弹出毛玻璃确认窗 + TTS 播报，同步等待结果。
    /// 这是一个同步回调，内部将工作 post 到 UI 线程并阻塞等待，不会造成 UI 死锁。
    /// </summary>
    private bool OnDangerousConfirmation(VoiceCommand command)
    {
        var tcs = new TaskCompletionSource<bool>();
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var actionName = command.DangerousActionName ?? "该";
                _speechService.EnqueueSpeechQueue($"是否确认执行{actionName}操作？");
                var win = new VoiceControlConfirmWindow(command);
                await win.ShowDialog(App.GetService<MainWindow>());
                tcs.TrySetResult(win.Confirmed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VoiceControl] 二次确认窗异常，按拒绝处理");
                tcs.TrySetResult(false);
            }
        });
        try { return tcs.Task.GetAwaiter().GetResult(); }
        catch { return false; }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try { _orchestrator?.Dispose(); } catch { /* ignore */ }
        try { _voiceLogger.Dispose(); } catch { /* ignore */ }
        return Task.CompletedTask;
    }

    private static IReadOnlyDictionary<string, string> DefaultAppLaunchMap() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["记事本"] = "notepad",
            ["计算器"] = "calc",
            ["命令提示符"] = "cmd",
            ["画图"] = "mspaint",
            ["浏览器"] = "https://www.bing.com",
            ["默认浏览器"] = "https://www.bing.com",
            ["设置"] = "ms-settings:",
            ["文件资源管理器"] = "explorer"
        };

    /// <summary>
    /// 将后台识别线程调度到 Avalonia UI 线程的 SynchronizationContext 适配器。
    /// </summary>
    private sealed class AvaloniaSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
            => Dispatcher.UIThread.Post(() => d(state), DispatcherPriority.Normal);

        public override void Send(SendOrPostCallback d, object? state)
            => Dispatcher.UIThread.InvokeAsync(() => d(state)).Wait();
    }
}
