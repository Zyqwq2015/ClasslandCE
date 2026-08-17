using ClassIsland.Services.VoiceControl.Abstractions;
using ClassIsland.Services.VoiceControl.Models;

namespace ClassIsland.Services.VoiceControl.Abstractions;

/// <summary>
/// UI 状态绑定接口。主界面的麦克风状态指示器与状态文本通过该接口与语音控制核心联动。
/// 设计为平台无关（VM 不含 WPF/Avalonia 专属类型），便于在 WPF 或 Avalonia 中复用。
/// </summary>
public interface IVoiceStatusViewModel
{
    /// <summary>当前整体状态（待机/监听/识别中/执行命令）。</summary>
    VoiceControlState State { get; }

    /// <summary>最近一次识别到的命令文本（用于展示）。</summary>
    string LastRecognizedText { get; }

    /// <summary>最近一次执行结果描述。</summary>
    string LastResultMessage { get; }

    /// <summary>语音监听是否启用（绑定设置面板开关，默认关闭）。</summary>
    bool IsEnabled { get; set; }

    /// <summary>诊断/运行状态描述（未启用、启动失败原因、麦克风无输入等）。</summary>
    string StatusMessage { get; set; }

    /// <summary>设置状态，触发 UI 动画切换。</summary>
    void SetState(VoiceControlState state);

    /// <summary>写入诊断状态文本（设置页「运行状态」卡片实时展示）。</summary>
    void NotifyStatus(string message);

    /// <summary>记录识别文本。</summary>
    void NotifyRecognized(string text);

    /// <summary>记录执行结果。</summary>
    void NotifyResult(VoiceCommandResult result);
}
