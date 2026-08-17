using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models.ComponentSettings;

/// <summary>
/// 摄像头画面采集组件设置
/// </summary>
public class CameraFeedComponentSettings : ObservableRecipient
{
    private int _deviceIndex = 0;
    private bool _mirrored = false;

    /// <summary>摄像头设备索引</summary>
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

    /// <summary>是否镜像显示</summary>
    public bool Mirrored
    {
        get => _mirrored;
        set
        {
            if (value == _mirrored) return;
            _mirrored = value;
            OnPropertyChanged();
        }
    }
}