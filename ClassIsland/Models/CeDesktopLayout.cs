using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models;

/// <summary>
/// 桌面组件布局布局（Classland CE）
/// </summary>
public class CeDesktopLayout
{
    public ObservableCollection<CeDesktopItem> Items { get; set; } = new();
}

/// <summary>
/// 单个桌面组件项
/// </summary>
public partial class CeDesktopItem : ObservableObject
{
    private double _x;
    private double _y;
    private double _width = 200;
    private double _height = 150;
    private string _componentId = "";
    private object? _settings;
    private string _id = Guid.NewGuid().ToString();

    /// <summary>布局项唯一 ID</summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>画布 X 坐标</summary>
    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    /// <summary>画布 Y 坐标</summary>
    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    /// <summary>组件宽度</summary>
    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    /// <summary>组件高度</summary>
    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    /// <summary>组件类型 Guid</summary>
    public string ComponentId
    {
        get => _componentId;
        set => SetProperty(ref _componentId, value);
    }

    /// <summary>组件自定义设置</summary>
    public object? Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }
}