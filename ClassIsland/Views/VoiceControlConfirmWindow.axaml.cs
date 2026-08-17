using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassIsland.Services.VoiceControl.Models;

namespace ClassIsland.Views;

/// <summary>
/// 高危指令（重启/关机）二次确认悬浮窗。毛玻璃质感，带 15 秒超时兜底，
/// 绝不自动执行高危操作。语音播报由 VoiceControlService 通过 ISpeechService 完成。
/// </summary>
public partial class VoiceControlConfirmWindow : Window
{
    public bool Confirmed { get; private set; }

    private readonly DispatcherTimer _timer;
    private int _remain = 15;

    public VoiceControlConfirmWindow(VoiceCommand command)
    {
        InitializeComponent();
        MessageText.Text = $"是否确认执行{command.DangerousActionName ?? "该"}操作？";

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _remain--;
            CountdownBar.Value = _remain;
            if (_remain <= 0)
            {
                _timer.Stop();
                Confirmed = false;
                Close();
            }
        };
        _timer.Start();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Confirmed = false;
        Close();
    }
}
