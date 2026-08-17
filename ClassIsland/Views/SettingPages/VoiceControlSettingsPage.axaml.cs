using System.ComponentModel;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Services;
using ClassIsland.ViewModels.SettingsPages;

namespace ClassIsland.Views.SettingPages;

/// <summary>
/// Classland CE 语音唤醒控制设置页
/// </summary>
[Group("classisland.general")]
[SettingsPageInfo("classland-ce-voice", "语音控制", "\uE720", "\uE720", SettingsPageCategory.Internal)]
public partial class VoiceControlSettingsPage : SettingsPageBase
{
    public VoiceControlSettingsViewModel ViewModel { get; } = App.GetService<VoiceControlSettingsViewModel>();

    public VoiceControlSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    // 启用开关 / 唤醒词 / 远场阈值等参数在启动时由语音引擎读取，运行时变更需重启生效。
    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsService.Settings.CeIsVoiceControlEnabled)
            or nameof(SettingsService.Settings.CeVoiceWakeWord)
            or nameof(SettingsService.Settings.CeVoiceMinWakeConfidence))
        {
            RequestRestart();
        }
    }

    private void VoiceControlSettingsPage_OnLoaded(object? sender, RoutedEventArgs e)
    {
        ViewModel.SettingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
    }

    private void VoiceControlSettingsPage_OnUnloaded(object? sender, RoutedEventArgs e)
    {
        ViewModel.SettingsService.Settings.PropertyChanged -= SettingsOnPropertyChanged;
    }
}
