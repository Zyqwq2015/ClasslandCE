using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 课件快捷方式组件的设置控件
/// </summary>
public partial class LessonShortcutComponentSettingsControl : ComponentBase<LessonShortcutComponentSettings>
{
    public LessonShortcutComponentSettingsControl()
    {
        InitializeComponent();
    }
}