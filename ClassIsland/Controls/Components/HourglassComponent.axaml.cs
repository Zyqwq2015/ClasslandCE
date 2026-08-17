using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 时间沙漏组件：精致优雅的 3D 玻璃沙漏。
/// 自绘渲染 + 画刷/静态几何缓存，性能高效，不会卡死 UI。
/// </summary>
[ComponentInfo("88CC3BF3-98BD-4BF9-B3B2-C66E042C7B0B", "时间沙漏", "\uE823",
    "3D 立体玻璃沙漏倒计时，可暂停、重置、自动循环。")]
public partial class HourglassComponent : ComponentBase<HourglassComponentSettings>
{
    private DispatcherTimer? _timer;
    private double _remaining;
    private bool _running;
    private int _lastWholeSecond = -1;

    public HourglassComponent()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _remaining = Settings.RemainingSeconds > 0 ? Settings.RemainingSeconds : Settings.DurationSeconds;
        _running = Settings.Started;

        if (Renderer != null)
        {
            Renderer.Duration = Settings.DurationSeconds;
            Renderer.Remaining = _remaining;
        }

        UpdateButtonText();

        // Timer 100ms（10fps 足够流畅且低开销）
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Tick();
        if (_running)
            _timer.Start();

        UpdateTimeText();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
        Settings.RemainingSeconds = Math.Max(0, _remaining);
        Settings.Started = _running;
    }

    private void Tick()
    {
        if (!_running) return;

        _remaining -= 0.1;

        if (_remaining <= 0)
        {
            if (Settings.AutoRestart)
            {
                _remaining = Settings.DurationSeconds;
            }
            else
            {
                _remaining = 0;
                _running = false;
                _timer?.Stop();
                Settings.Started = false;
                UpdateButtonText();
            }
        }

        if (Renderer != null)
            Renderer.Remaining = _remaining;

        UpdateTimeText();
    }

    private void UpdateTimeText()
    {
        if (TimeText == null) return;
        var whole = (int)Math.Ceiling(Math.Max(0, _remaining));
        if (whole == _lastWholeSecond) return; // 整秒不变则不刷新（核心优化：消除每帧 TextBlock 布局重排）
        _lastWholeSecond = whole;
        var ts = TimeSpan.FromSeconds(whole);
        TimeText.Text = ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
    }

    private void ToggleButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_running)
        {
            _running = false;
            _timer?.Stop();
            Settings.Started = false;
        }
        else
        {
            if (_remaining <= 0)
                _remaining = Settings.DurationSeconds;
            _running = true;
            Settings.Started = true;
            _timer?.Start();
        }
        UpdateButtonText();
    }

    private void ResetButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _running = false;
        _timer?.Stop();
        _remaining = Settings.DurationSeconds;
        Settings.Started = false;
        Settings.RemainingSeconds = Settings.DurationSeconds;
        _lastWholeSecond = -1; // 强制下次重算文本
        if (Renderer != null)
            Renderer.Remaining = _remaining;
        UpdateButtonText();
        UpdateTimeText();
    }

    private void UpdateButtonText()
    {
        if (ToggleButton != null)
            ToggleButton.Content = _running ? "暂停" : "开始";
    }
}