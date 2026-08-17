using System;
using Avalonia.Controls;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 番茄时钟组件：25 分钟专注 + 5 分钟休息
/// </summary>
[ComponentInfo("3FB8C782-63E0-4DC1-AA56-B8074200E490", "番茄时钟", "\uEB9C", "番茄工作法：专注 25 分钟，休息 5 分钟。")]
public partial class PomodoroComponent : ComponentBase<PomodoroComponentSettings>
{
    private DispatcherTimer? _timer;

    public PomodoroComponent()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateUi();
        if (Settings.Running)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    private void Tick()
    {
        Settings.RemainingSeconds -= 1;
        if (Settings.RemainingSeconds <= 0)
        {
            OnPhaseComplete();
        }
        UpdateUi();
    }

    private void OnPhaseComplete()
    {
        if (Settings.Session == 0)
        {
            // 工作完成
            Settings.CompletedPomodoros++;
            Settings.Session = 1;
            Settings.RemainingSeconds = Settings.BreakMinutes * 60;
        }
        else
        {
            // 休息完成
            Settings.Session = 0;
            Settings.RemainingSeconds = Settings.WorkMinutes * 60;
        }

        if (!Settings.AutoStartNext)
        {
            Settings.Running = false;
            _timer?.Stop();
        }
    }

    private void UpdateUi()
    {
        TimeText.Text = TimeSpan.FromSeconds(Math.Max(0, Settings.RemainingSeconds)).ToString(@"mm\:ss");
        SessionText.Text = Settings.Session == 0 ? "\U0001F345 工作" : "\u2615 \u4F11\u606F";
        CountText.Text = Settings.CompletedPomodoros.ToString();
        StartPauseButton.Content = Settings.Running ? "暂停" : "开始";
    }

    private void StartPauseButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Settings.Running)
        {
            Settings.Running = false;
            _timer?.Stop();
        }
        else
        {
            if (Settings.RemainingSeconds <= 0)
                Settings.RemainingSeconds = Settings.Session == 0 ? Settings.WorkMinutes * 60 : Settings.BreakMinutes * 60;
            Settings.Running = true;
            _timer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick -= (_, _) => Tick(); // 防重复
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
        }
        UpdateUi();
    }

    private void ResetButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Settings.Running = false;
        _timer?.Stop();
        Settings.RemainingSeconds = Settings.Session == 0 ? Settings.WorkMinutes * 60 : Settings.BreakMinutes * 60;
        UpdateUi();
    }

    private void SkipButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Settings.RemainingSeconds = 0;
        OnPhaseComplete();
        UpdateUi();
    }
}