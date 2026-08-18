using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace ClassIsland.Converters;

/// <summary>
/// 将图片文件路径（jpg / png / ico / bmp）转换为 Avalonia Bitmap，供桌面卡片图标显示。
/// 路径为空、不存在或加载失败时返回 null，由界面回退到默认图标。
/// </summary>
public class FilePathToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;
        path = Environment.ExpandEnvironmentVariables(path);
        try
        {
            if (!File.Exists(path))
                return null;
            using var img = System.Drawing.Image.FromFile(path);
            using var ms = new MemoryStream();
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            return new Bitmap(ms);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CE] 加载桌面卡片图标失败: {path} - {ex.Message}");
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}
