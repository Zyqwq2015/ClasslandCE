using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 音量检测组件的设置控件
/// </summary>
public partial class VolumeLevelComponentSettingsControl : ComponentBase<VolumeLevelComponentSettings>
{
    public VolumeLevelComponentSettingsControl()
    {
        InitializeComponent();
    }
}