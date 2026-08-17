using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 番茄时钟组件的设置控件
/// </summary>
public partial class PomodoroComponentSettingsControl : ComponentBase<PomodoroComponentSettings>
{
    public PomodoroComponentSettingsControl()
    {
        InitializeComponent();
    }
}