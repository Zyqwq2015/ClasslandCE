using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 摄像头画面组件的设置控件
/// </summary>
public partial class CameraFeedComponentSettingsControl : ComponentBase<CameraFeedComponentSettings>
{
    public CameraFeedComponentSettingsControl()
    {
        InitializeComponent();
    }
}