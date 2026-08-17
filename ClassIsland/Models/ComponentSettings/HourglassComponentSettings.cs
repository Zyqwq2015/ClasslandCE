using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models.ComponentSettings;

/// <summary>
/// 时间沙漏组件设置
/// </summary>
public class HourglassComponentSettings : ObservableRecipient
{
    private int _durationSeconds = 60;
    private bool _autoRestart = true;
    private bool _started = false;

    /// <summary>
    /// 一次倒计时时长（秒）
    /// </summary>
    public int DurationSeconds
    {
        get => _durationSeconds;
        set
        {
            if (value == _durationSeconds) return;
            _durationSeconds = Math.Clamp(value, 5, 3600);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 倒计时结束后是否自动重新开始
    /// </summary>
    public bool AutoRestart
    {
        get => _autoRestart;
        set
        {
            if (value == _autoRestart) return;
            _autoRestart = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否正在倒计时（编辑时保存，重启后自动继续）
    /// </summary>
    public bool Started
    {
        get => _started;
        set
        {
            if (value == _started) return;
            _started = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 上次剩余时长（秒），用于组件重载时恢复
    /// </summary>
    public double RemainingSeconds { get; set; } = 60;
}