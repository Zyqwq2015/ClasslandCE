using System;
using Microsoft.Win32;

namespace ClassIsland.Services;

/// <summary>
/// 桌面壁纸辅助类（Classland CE）
/// </summary>
public static class WallpaperHelper
{
    /// <summary>
    /// 获取当前 Windows 桌面壁纸路径。
    /// <para>读取注册表 HKCU\Control Panel\Desktop 的 WallPaper 项。</para>
    /// </summary>
    /// <returns>壁纸文件路径；读取失败时返回 null。</returns>
    public static string? GetCurrentWallpaperPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            return key?.GetValue("WallPaper") as string;
        }
        catch (Exception)
        {
            return null;
        }
    }
}