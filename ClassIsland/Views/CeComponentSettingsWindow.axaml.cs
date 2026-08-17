using System;
using Avalonia.Controls;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Models;
using ClassIsland.Services;

namespace ClassIsland.Views;

/// <summary>
/// 桌面组件设置窗口（Classland CE）：直接承载组件的设置控件。
/// </summary>
public partial class CeComponentSettingsWindow : Window
{
    public CeComponentSettingsWindow(CeDesktopLayoutService layoutService, CeDesktopItem item)
    {
        InitializeComponent();
        Title = "桌面组件设置";
        var component = layoutService.CreateComponentView(item, isSettings: true);
        if (component != null)
        {
            SettingsHost.Content = component;
        }
        else
        {
            SettingsHost.Content = new TextBlock { Text = "该组件没有设置界面", Margin = new Avalonia.Thickness(12) };
        }
    }

    private void CloseButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }
}