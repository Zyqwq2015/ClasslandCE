using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 时间沙漏组件：可视化的倒计时沙漏动画
/// </summary>
[ComponentInfo("88CC3BF3-98BD-4BF9-B3B2-C66E042C7B0B", "时间沙漏", "\uE823", "可视化的沙漏倒计时，可暂停、重置、自动循环。")]
public partial class HourglassComponent : ComponentBase<HourglassComponentSettings>
{
    private DispatcherTimer? _timer;
    private double _remaining;
    private bool _running;

    public HourglassComponent()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // 恢复上次状态
        _remaining = Settings.RemainingSeconds > 0 ? Settings.RemainingSeconds : Settings.DurationSeconds;
        _running = Settings.Started;
        UpdateButtonText();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => Tick();
        if (_running) _timer.Start();
        Render();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
        // 保存状态供组件重载时恢复
        Settings.RemainingSeconds = Math.Max(0, _remaining);
        Settings.Started = _running;
    }

    private void Tick()
    {
        if (!_running) return;
        _remaining -= 0.05;
        if (_remaining <= 0)
        {
            _remaining = 0;
            if (Settings.AutoRestart)
            {
                _remaining = Settings.DurationSeconds;
            }
            else
            {
                _running = false;
                _timer?.Stop();
                UpdateButtonText();
            }
        }
        Render();
    }

    private void Render()
    {
        var p = _remaining <= 0 ? 0.0 : Math.Clamp(_remaining / Settings.DurationSeconds, 0.0, 1.0);
        // 上瓶沙：从顶部向下，随剩余比例减小
        var topY = 5 + 43 * (1 - p);
        SandTop.Data = StreamGeometry.Parse(
            $"M 20 {topY:F1} L 120 {topY:F1} L 105 48 L 35 48 Z");
        // 下瓶沙：从底部向上，随已流逝比例增大
        var bottomTop = 91 - 15 * (1 - p);
        SandBottom.Data = StreamGeometry.Parse(
            $"M 35 {bottomTop:F1} L 105 {bottomTop:F1} L 105 91 L 35 91 Z");
        TimeText.Text = FormatTime(Math.Max(0, _remaining));
    }

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Ceiling(seconds));
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
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
            if (_remaining <= 0) _remaining = Settings.DurationSeconds; // 结束后再开始则重来
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
        UpdateButtonText();
        Render();
    }

    private void UpdateButtonText()
    {
        if (ToggleButton != null)
            ToggleButton.Content = _running ? "暂停" : "开始";
    }
}