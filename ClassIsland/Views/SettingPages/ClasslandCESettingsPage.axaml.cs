using System.ComponentModel;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;

namespace ClassIsland.Views.SettingPages;

/// <summary>
/// ClassIsland CE 增强功能设置页
/// </summary>
[Group("classisland.general")]
[SettingsPageInfo("classland-ce", "ClassIsland CE", "\uE8CA", "\uE8CA", SettingsPageCategory.Internal)]
public partial class ClasslandCESettingsPage : SettingsPageBase
{
    public ClasslandCESettingsViewModel ViewModel { get; } = IAppHost.GetService<ClasslandCESettingsViewModel>();

    public ClasslandCESettingsPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsService.Settings.CeIsVoiceAssistantEnabled))
        {
            RequestRestart();
        }
    }

    private void ClasslandCESettingsPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.SettingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
    }

    private void ClasslandCESettingsPage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.SettingsService.Settings.PropertyChanged -= SettingsOnPropertyChanged;
    }
}
