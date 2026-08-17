using System;
using ClassIsland.Services.VoiceControl.Abstractions;
using ClassIsland.Services.VoiceControl.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Services.VoiceControl.ViewModels;

/// <summary>
/// 语音控制 UI 状态视图模型。平台无关（不含 WPF/Avalonia 类型），
/// 通过 <see cref="IVoiceStatusViewModel"/> 与主界面绑定。
/// </summary>
public partial class VoiceStatusViewModel : ObservableObject, IVoiceStatusViewModel
{
    [ObservableProperty]
    private VoiceControlState _state = VoiceControlState.Standby;

    [ObservableProperty]
    private string _lastRecognizedText = string.Empty;

    [ObservableProperty]
    private string _lastResultMessage = string.Empty;

    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>诊断/运行状态描述（未启用、启动失败原因、当前识别语言等），供设置页与指示器展示。</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public void SetState(VoiceControlState state) => State = state;

    public void NotifyStatus(string message) => StatusMessage = message;

    public void NotifyRecognized(string text) => LastRecognizedText = text;

    public void NotifyResult(VoiceCommandResult result)
    {
        if (result != null)
            LastResultMessage = result.Message;
    }
}
