using ClassIsland.Services;
using ClassIsland.Services.VoiceControl.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.ViewModels.SettingsPages;

public partial class VoiceControlSettingsViewModel : ObservableRecipient
{
    public SettingsService SettingsService { get; }

    /// <summary>语音控制运行状态（未启用/启动失败原因/当前识别语言），实时反映 VoiceControlService 诊断信息。</summary>
    public VoiceStatusViewModel Status { get; }

    public VoiceControlSettingsViewModel(SettingsService settingsService, VoiceControlService voiceControlService)
    {
        SettingsService = settingsService;
        Status = voiceControlService.StatusViewModel;
    }
}
