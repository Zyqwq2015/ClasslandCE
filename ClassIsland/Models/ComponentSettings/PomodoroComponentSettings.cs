using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models.ComponentSettings;

/// <summary>
/// 番茄时钟组件设置
/// </summary>
public class PomodoroComponentSettings : ObservableRecipient
{
    private int _workMinutes = 25;
    private int _breakMinutes = 5;
    private bool _autoStartNext = true;
    private int _session = 0; // 当前阶段：0=工作 1=休息
    private int _completedPomodoros = 0;
    private double _remainingSeconds = 25 * 60;
    private bool _running = false;

    /// <summary>工作时长（分钟）</summary>
    public int WorkMinutes
    {
        get => _workMinutes;
        set
        {
            if (value == _workMinutes) return;
            _workMinutes = Math.Clamp(value, 1, 120);
            OnPropertyChanged();
        }
    }

    /// <summary>休息时长（分钟）</summary>
    public int BreakMinutes
    {
        get => _breakMinutes;
        set
        {
            if (value == _breakMinutes) return;
            _breakMinutes = Math.Clamp(value, 1, 120);
            OnPropertyChanged();
        }
    }

    /// <summary>当前阶段结束后是否自动开始下一阶段</summary>
    public bool AutoStartNext
    {
        get => _autoStartNext;
        set
        {
            if (value == _autoStartNext) return;
            _autoStartNext = value;
            OnPropertyChanged();
        }
    }

    /// <summary>当前阶段：0=工作 1=休息</summary>
    public int Session
    {
        get => _session;
        set
        {
            if (value == _session) return;
            _session = value;
            OnPropertyChanged();
        }
    }

    /// <summary>完成的番茄数</summary>
    public int CompletedPomodoros
    {
        get => _completedPomodoros;
        set
        {
            if (value == _completedPomodoros) return;
            _completedPomodoros = value;
            OnPropertyChanged();
        }
    }

    /// <summary>剩余秒数</summary>
    public double RemainingSeconds
    {
        get => _remainingSeconds;
        set
        {
            if (Math.Abs(value - _remainingSeconds) < 0.01) return;
            _remainingSeconds = value;
            OnPropertyChanged();
        }
    }

    /// <summary>是否运行中</summary>
    public bool Running
    {
        get => _running;
        set
        {
            if (value == _running) return;
            _running = value;
            OnPropertyChanged();
        }
    }
}