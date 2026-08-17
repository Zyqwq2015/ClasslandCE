using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 时间沙漏组件的设置控件
/// </summary>
public partial class HourglassComponentSettingsControl : ComponentBase<HourglassComponentSettings>
{
    public HourglassComponentSettingsControl()
    {
        InitializeComponent();
    }
}