using System;
using ClassIsland.Services.VoiceControl.Models;

namespace ClassIsland.Services.VoiceControl.Abstractions;

/// <summary>
/// 语音命令解析器接口。将识别出的纯文本字符串解析为结构化 <see cref="VoiceCommand"/> 意图。
/// </summary>
public interface ICommandParser
{
    /// <summary>
    /// 解析文本。无法识别时返回 <see cref="VoiceIntent.Unknown"/> 的命令。
    /// </summary>
    /// <param name="text">来自唤醒引擎的识别文本。</param>
    /// <returns>结构化指令；解析失败也不会返回 null。</returns>
    VoiceCommand Parse(string? text);

    /// <summary>
    /// 软件名 -> 启动方式（exe 路径 / 协议 / 系统别名）映射，供 LaunchApp 使用。
    /// 通常在启动时从 JSON 配置载入。
    /// </summary>
    void LoadAppLaunchMap(System.Collections.Generic.IReadOnlyDictionary<string, string> map);
}
