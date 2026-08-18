using System.Collections.Generic;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 桌面卡片设置控件：标题、图标（jpg/png/ico）、目标路径、启动参数。
/// </summary>
public partial class ShortcutCardComponentSettingsControl : ComponentBase<ShortcutCardComponentSettings>
{
    public ShortcutCardComponentSettingsControl()
    {
        InitializeComponent();

        IconBrowser.FileTypes = new List<FilePickerFileType>
        {
            new("图片文件") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.ico", "*.bmp" } },
            FileBrowserButton.TypeAll
        };
        TargetBrowser.FileTypes = new List<FilePickerFileType>
        {
            FileBrowserButton.TypeApplication,
            FileBrowserButton.TypeAll
        };
    }

    private void IconBrowser_OnFileSelected(object? sender, System.EventArgs e)
    {
        if (Settings != null) Settings.IconPath = IconBrowser.CurrentPath;
    }

    private void TargetBrowser_OnFileSelected(object? sender, System.EventArgs e)
    {
        if (Settings != null) Settings.TargetPath = TargetBrowser.CurrentPath;
    }
}
