using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.SpeechService;
using ClassIsland.Helpers;
using ClassIsland.Shared.Models.Profile;
using ClassIsland.Views;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services;

/// <summary>
/// Classland CE 语音助手 — 快捷键唤醒 + TTS 反馈。
/// </summary>
/// <remarks>
/// 唤醒方式：Ctrl+Alt+C 打开命令输入框，输入指令后 TTS 播报结果。
/// 完全离线，无需语音识别硬件或网络。
/// 指令：下一节课、今天课表、现在上什么课、现在几点、打开浏览器、打开设置等。
/// </remarks>
public class VoiceAssistantService : IHostedService
{
    private SettingsService SettingsService { get; }
    private ILessonsService LessonsService { get; }
    private IExactTimeService ExactTimeService { get; }
    private ISpeechService SpeechService { get; }
    private ILogger<VoiceAssistantService> Logger { get; }

    private bool _isEnabled;
    private bool _isDialogOpen;

    public event EventHandler<string>? StatusChanged;

    public VoiceAssistantService(
        SettingsService settingsService,
        ILessonsService lessonsService,
        IExactTimeService exactTimeService,
        ISpeechService speechService,
        ILogger<VoiceAssistantService> logger)
    {
        SettingsService = settingsService;
        LessonsService = lessonsService;
        ExactTimeService = exactTimeService;
        SpeechService = speechService;
        Logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _isEnabled = SettingsService.Settings.CeIsVoiceAssistantEnabled;
        if (!_isEnabled) return Task.CompletedTask;

        // 注册全局快捷键: Ctrl+Alt+C
        try
        {
            // 在 Avalonia 中注册全局快捷键需要平台特定实现
            // 这里通过主窗口的 KeyDown 事件监听
            Dispatcher.UIThread.Post(() =>
            {
                var mainWindow = App.GetService<MainWindow>();
                if (mainWindow != null)
                {
                    mainWindow.KeyDown += OnMainWindowKeyDown;
                }
            });
            Logger.LogInformation("[VoiceAssistant] 快捷键已注册 (Ctrl+Alt+C)");
            StatusChanged?.Invoke(this, "按 Ctrl+Alt+C 打开命令输入");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[VoiceAssistant] 注册快捷键失败");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _isEnabled = false;
        return Task.CompletedTask;
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isEnabled) return;
        // Ctrl+Alt+C
        if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            e.Handled = true;
            if (!_isDialogOpen)
                _ = ShowCommandDialogAsync();
        }
    }

    private async Task ShowCommandDialogAsync()
    {
        _isDialogOpen = true;
        try
        {
            var mainWindow = App.GetService<MainWindow>();
            if (mainWindow == null) return;

            // 使用 Avalonia 的 TextInputDialog 或简化版输入框
            var dialog = new TextBox
            {
                Watermark = "输入指令（如：下一节课、今天课表、打开浏览器）",
                MinWidth = 400,
                Margin = new Thickness(16)
            };

            var window = new Window
            {
                Title = "Classland CE 命令输入",
                Width = 500,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new StackPanel
                {
                    Margin = new Thickness(16),
                    Children =
                    {
                        new TextBlock { Text = "输入指令后按 Enter 执行：", Margin = new Thickness(0, 0, 0, 8) },
                        dialog,
                        new TextBlock
                        {
                            Text = "支持指令：下一节课、今天课表、现在上什么课、现在几点、打开浏览器、打开设置、" +
                                   "打开记事本、打开计算器、显示课表、隐藏课表、今天星期几、谢谢",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            FontSize = 12,
                            Foreground = Avalonia.Media.Brushes.Gray,
                            Margin = new Thickness(0, 8, 0, 0)
                        }
                    }
                }
            };

            dialog.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    var command = dialog.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(command))
                    {
                        ExecuteCommand(command);
                    }
                    window.Close();
                }
                else if (e.Key == Key.Escape)
                {
                    window.Close();
                }
            };

            window.Closing += (_, _) => { _isDialogOpen = false; };
            await window.ShowDialog(mainWindow);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[VoiceAssistant] 打开对话框失败");
        }
        finally
        {
            _isDialogOpen = false;
        }
    }

    public void ExecuteCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();
        Logger.LogInformation("[VoiceAssistant] 执行指令: {Cmd}", command);

        try
        {
            switch (command)
            {
                case var s when s.Contains("下一节") || s.Contains("下节课"):
                    HandleNextClass();
                    break;
                case var s when s.Contains("今天课表") || s.Contains("今天有什么课"):
                    HandleTodaySchedule();
                    break;
                case var s when s.Contains("现在上什么课") || s.Contains("是什么课") || s.Contains("当前课"):
                    HandleCurrentClass();
                    break;
                case var s when s.Contains("现在几点") || s.Contains("几点了"):
                    HandleCurrentTime();
                    break;
                case var s when s.Contains("打开设置"):
                    HandleOpenSettings();
                    break;
                case var s when s.Contains("打开浏览器") || s.Contains("打开网页"):
                    HandleOpenBrowser();
                    break;
                case var s when s.Contains("显示课表") || s.Contains("显示主界面"):
                    HandleShowMainWindow();
                    break;
                case var s when s.Contains("隐藏课表") || s.Contains("最小化"):
                    HandleHideMainWindow();
                    break;
                case var s when s.Contains("记事本"):
                    HandleRunApp("notepad.exe");
                    break;
                case var s when s.Contains("计算器"):
                    HandleRunApp("calc.exe");
                    break;
                case var s when s.Contains("星期几"):
                    HandleWeekDay();
                    break;
                case var s when s.Contains("谢谢") || s.Contains("谢谢你"):
                    SpeechService.EnqueueSpeechQueue("不客气，有需要随时叫我。");
                    break;
                default:
                    SpeechService.EnqueueSpeechQueue("抱歉，没有识别到这个指令。请按 Ctrl+Alt+C 查看可用指令列表。");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[VoiceAssistant] 执行指令失败: {Cmd}", command);
            SpeechService.EnqueueSpeechQueue("执行指令时出错了。");
        }
    }

    private void HandleNextClass()
    {
        var next = LessonsService.NextClassSubject;
        if (next == null || next == Subject.Fallback)
        {
            SpeechService.EnqueueSpeechQueue("今天没有下一节课了。");
            return;
        }
        var item = LessonsService.NextClassTimeLayoutItem;
        var template = SettingsService.Settings.CeSpeechTemplate;
        var speech = SpeechTemplateHelper.Render(template, next, item, 0);
        SpeechService.EnqueueSpeechQueue(speech);
    }

    private void HandleTodaySchedule()
    {
        var plan = LessonsService.CurrentClassPlan;
        if (plan?.Classes == null || plan.Classes.Count == 0)
        {
            SpeechService.EnqueueSpeechQueue("今天没有课表信息。");
            return;
        }

        var profile = App.GetService<IProfileService>().Profile;
        var schedule = new System.Collections.Generic.List<string>();
        for (int i = 0; i < plan.Classes.Count; i++)
        {
            var classInfo = plan.Classes[i];
            if (classInfo.SubjectId == Guid.Empty || !classInfo.IsEnabled)
                continue;
            if (!profile.Subjects.TryGetValue(classInfo.SubjectId, out var subject) || subject == null)
                continue;
            if (subject == Subject.Fallback || string.IsNullOrWhiteSpace(subject.Name))
                continue;

            var slot = classInfo.CurrentTimeLayoutItem;
            var timeText = slot != TimeLayoutItem.Empty
                ? $"（{slot.StartTime.Hours}:{slot.StartTime.Minutes:D2}）"
                : "";
            schedule.Add($"第{i + 1}节{subject.Name}{timeText}");
        }

        if (schedule.Count == 0)
        {
            SpeechService.EnqueueSpeechQueue("今天没有排课。");
            return;
        }

        SpeechService.EnqueueSpeechQueue("今天的课程有：" + string.Join("，", schedule));
    }

    private void HandleCurrentClass()
    {
        var current = LessonsService.CurrentSubject;
        if (current == null || current == Subject.Fallback)
        {
            SpeechService.EnqueueSpeechQueue("现在是课间休息时间。");
            return;
        }
        var item = LessonsService.CurrentTimeLayoutItem;
        var end = item.EndTime;
        var teacher = !string.IsNullOrWhiteSpace(current.GetFirstName()) ? $"由{current.GetFirstName()}老师任教" : "";
        SpeechService.EnqueueSpeechQueue(
            $"现在是{current.Name}，{teacher}{end.Hours}点{end.Minutes:D2}分下课。");
    }

    private void HandleCurrentTime()
    {
        var now = ExactTimeService.GetCurrentLocalDateTime();
        SpeechService.EnqueueSpeechQueue($"现在是{now.Hour}点{now.Minute:D2}分。");
    }

    private void HandleOpenSettings()
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var window = App.GetService<SettingsWindowNew>();
                window?.Show();
                window?.Activate();
            }
            catch
            {
                // SettingsWindowNew 可能不可用
            }
        });
        SpeechService.EnqueueSpeechQueue("已打开设置。");
    }

    private void HandleOpenBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.bing.com") { UseShellExecute = true });
            SpeechService.EnqueueSpeechQueue("已打开浏览器。");
        }
        catch
        {
            SpeechService.EnqueueSpeechQueue("无法打开浏览器。");
        }
    }

    private void HandleShowMainWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var mw = App.GetService<MainWindow>();
                if (mw != null)
                {
                    mw.Show();
                    mw.Activate();
                    mw.WindowState = WindowState.Normal;
                }
            }
            catch { }
        });
        SpeechService.EnqueueSpeechQueue("已显示主界面。");
    }

    private void HandleHideMainWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var mw = App.GetService<MainWindow>();
                if (mw != null)
                    mw.WindowState = WindowState.Minimized;
            }
            catch { }
        });
        SpeechService.EnqueueSpeechQueue("已最小化。");
    }

    private void HandleRunApp(string appName)
    {
        try
        {
            Process.Start(new ProcessStartInfo(appName) { UseShellExecute = true });
            SpeechService.EnqueueSpeechQueue($"已打开{appName.Replace(".exe", "")}。");
        }
        catch
        {
            SpeechService.EnqueueSpeechQueue($"无法打开{appName.Replace(".exe", "")}。");
        }
    }

    private void HandleWeekDay()
    {
        var now = ExactTimeService.GetCurrentLocalDateTime();
        var dayNames = new[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        SpeechService.EnqueueSpeechQueue($"今天是{dayNames[(int)now.DayOfWeek]}。");
    }
}