using System;
using System.Diagnostics;
using System.IO;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 课目课件快捷方式组件：点击打开课件/网址/程序
/// </summary>
[ComponentInfo("D80EBE4A-9F35-4D71-9184-FF3EA0926A2C", "课件快捷方式", "\uE8A5", "语数英科等课件快捷方式按钮，点击打开对应文件或网址。")]
public partial class LessonShortcutComponent : ComponentBase<LessonShortcutComponentSettings>
{
    public LessonShortcutComponent()
    {
        InitializeComponent();
    }

    private void OpenButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var target = Settings.TargetPath;
            if (string.IsNullOrWhiteSpace(target))
                return;

            // 环境变量展开（例如 %USERPROFILE%\Desktop\语文.pptx）
            target = Environment.ExpandEnvironmentVariables(target);

            var psi = new ProcessStartInfo()
            {
                FileName = target,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(Settings.Arguments))
                psi.Arguments = Settings.Arguments;

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            // 静默，桌面小组件内不弹窗打扰
            Debug.WriteLine($"[CE] 打开课件失败: {ex.Message}");
        }
    }
}