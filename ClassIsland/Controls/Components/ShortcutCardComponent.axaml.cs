using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 桌面卡片：可自定义图标与目标（文件 / 程序 / 网址）的桌面快捷方式卡片，点击即打开。
/// </summary>
[ComponentInfo("6F2C9D11-7E3A-4B58-9C2E-1D4F8A6B0C33", "桌面卡片", "\uE8D4", "可自定义图标（jpg/png/ico）与目标的桌面快捷方式卡片，点击打开文件、程序或网址。")]
public partial class ShortcutCardComponent : ComponentBase<ShortcutCardComponentSettings>
{
    public ShortcutCardComponent()
    {
        InitializeComponent();
    }

    private void OpenButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var target = Settings?.TargetPath;
            if (string.IsNullOrWhiteSpace(target))
                return;

            // 环境变量展开（例如 %USERPROFILE%\Desktop\语文.pptx）
            target = Environment.ExpandEnvironmentVariables(target);

            var psi = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(Settings?.Arguments))
                psi.Arguments = Settings.Arguments;

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            // 静默，桌面卡片内不弹窗打扰
            Debug.WriteLine($"[CE] 打开桌面卡片目标失败: {ex.Message}");
        }
    }
}
