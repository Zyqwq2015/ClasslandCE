using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ClassIsland.Services.VoiceControl.Abstractions;
using ClassIsland.Services.VoiceControl.Models;

namespace ClassIsland.Services.VoiceControl.Core;

/// <summary>
/// 基于正则表达式的语音命令解析器。识别四类意图：
/// 桌面操作、软件启动、系统电源（高危）、音量控制。
/// 解析失败不会抛异常，始终返回有效 <see cref="VoiceCommand"/>（Unknown）。
/// </summary>
public sealed class RegexCommandParser : ICommandParser
{
    private IReadOnlyDictionary<string, string> _appMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // 软件启动：打开/启动/运行 + 名称
    private static readonly Regex LaunchRegex =
        new(@"^\s*(?:打开|启动|运行|launch|open|start)\s*[:：]?\s*(.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 音量设置到具体数值：音量(到/设置到/调到)?N(百分之)? / set volume N
    private static readonly Regex VolumeSetRegex =
        new(@"(?:音量|声音|volume)\s*(?:设置到|调到|调为|调至|到|为)?\s*(\d{1,3})\s*(?:%|％|percent)?|set\s+volume\s+(\d{1,3})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public void LoadAppLaunchMap(IReadOnlyDictionary<string, string> map)
    {
        _appMap = map ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public VoiceCommand Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new VoiceCommand { Intent = VoiceIntent.Unknown, RawText = text ?? string.Empty };

        var raw = text.Trim();
        var lower = raw.ToLowerInvariant();

        // 1) 系统电源（高危，优先匹配）
        if (Regex.IsMatch(lower, @"(重启|重新启动|reboot|restart).*?(电脑|系统|机器|计算机)?"))
            return Cmd(VoiceIntent.RestartComputer, raw);
        if (Regex.IsMatch(lower, @"(关机|关闭电脑|power\s*off|shutdown).*?(电脑|系统|机器|计算机)?"))
            return Cmd(VoiceIntent.ShutdownComputer, raw);

        // 2) 音量设置（含数值，需在 调大/调小 之前）
        var m = VolumeSetRegex.Match(lower);
        if (m.Success)
        {
            var numStr = m.Groups[1].Success && m.Groups[1].Value.Length > 0
                ? m.Groups[1].Value
                : m.Groups[2].Value;
            if (int.TryParse(numStr, out var lvl))
                return Cmd(VoiceIntent.VolumeSet, raw, volumeLevel: Math.Clamp(lvl, 0, 100));
        }

        // 3) 音量动作
        if (lower.Contains("静音") || lower.Contains("mute"))
            return Cmd(VoiceIntent.VolumeMute, raw);
        if (lower.Contains("取消静音") || lower.Contains("解除静音") || lower.Contains("恢复声音")
            || lower.Contains("打开声音") || lower.Contains("unmute"))
            return Cmd(VoiceIntent.VolumeUnmute, raw);
        if (lower.Contains("调大") || lower.Contains("增大") || lower.Contains("加") || lower.Contains(" louder")
            || lower.Contains("大一点") || lower.Contains("升高"))
            return Cmd(VoiceIntent.VolumeUp, raw);
        if (lower.Contains("调小") || lower.Contains("减小") || lower.Contains("减") || lower.Contains(" quieter")
            || lower.Contains("小一点") || lower.Contains("降低"))
            return Cmd(VoiceIntent.VolumeDown, raw);

        // 4) 桌面操作
        if (lower.Contains("回到桌面") || lower.Contains("显示桌面") || lower.Contains("回到主屏幕")
            || lower.Contains("win+d") || lower.Contains("回到桌面") || lower.Contains("show desktop")
            || lower.Contains("桌面"))
            return Cmd(VoiceIntent.ShowDesktop, raw);
        if (lower.Contains("收起窗口") || lower.Contains("收起所有") || lower.Contains("最小化") || lower.Contains("minimize"))
            return Cmd(VoiceIntent.MinimizeWindows, raw);

        // 5) 软件启动（兜底）
        var lm = LaunchRegex.Match(raw);
        if (lm.Success)
        {
            var appName = lm.Groups[1].Value.Trim();
            if (appName.Length > 0)
                return Cmd(VoiceIntent.LaunchApp, raw, appName: ResolveApp(appName));
        }

        return new VoiceCommand { Intent = VoiceIntent.Unknown, RawText = raw };
    }

    /// <summary>
    /// 将口语软件名解析为可执行标识：优先查映射表，否则原样返回（执行器再按系统别名尝试）。
    /// </summary>
    private string ResolveApp(string spoken)
    {
        if (_appMap.TryGetValue(spoken, out var direct) && !string.IsNullOrWhiteSpace(direct))
            return direct;

        // 部分匹配：例如"网易云音乐"命中"网易云"
        foreach (var key in _appMap.Keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (spoken.Contains(key, StringComparison.OrdinalIgnoreCase)
                || key.Contains(spoken, StringComparison.OrdinalIgnoreCase))
                return _appMap[key]!;
        }
        return spoken;
    }

    private static VoiceCommand Cmd(VoiceIntent intent, string raw, string? appName = null, int? volumeLevel = null) =>
        new() { Intent = intent, RawText = raw, AppName = appName, VolumeLevel = volumeLevel };
}
