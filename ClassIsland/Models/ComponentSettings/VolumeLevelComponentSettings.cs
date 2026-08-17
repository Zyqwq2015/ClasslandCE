using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models.ComponentSettings;

/// <summary>
/// 音量检测组件设置
/// </summary>
public class VolumeLevelComponentSettings : ObservableRecipient
{
    private int _sensitivity = 60;
    private int _deviceIndex = 0;

    /// <summary>拾音灵敏度（0-100），越高越容易检测到声音</summary>
    public int Sensitivity
    {
        get => _sensitivity;
        set
        {
            if (value == _sensitivity) return;
            _sensitivity = Math.Clamp(value, 0, 100);
            OnPropertyChanged();
        }
    }

    /// <summary>麦克风设备索引</summary>
    public int DeviceIndex
    {
        get => _deviceIndex;
        set
        {
            if (value == _deviceIndex) return;
            _deviceIndex = Math.Max(0, value);
            OnPropertyChanged();
        }
    }
}