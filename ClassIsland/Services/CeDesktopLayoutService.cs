using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Models.Components;
using ClassIsland.Core.Services.Registry;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services;

/// <summary>
/// 桌面组件布局服务（Classland CE）
/// <para>管理壁纸层浮动组件的布局持久化与组件实例化。</para>
/// </summary>
public class CeDesktopLayoutService(ILogger<CeDesktopLayoutService> logger, IComponentsService componentsService)
{
    private ILogger<CeDesktopLayoutService> Logger { get; } = logger;
    private IComponentsService ComponentsService { get; } = componentsService;

    private static readonly string LayoutPath = Path.Combine(CommonDirectories.AppConfigPath, "CeDesktopLayout.json");

    private Models.CeDesktopLayout _layout = new();

    /// <summary>当前桌面组件布局</summary>
    public Models.CeDesktopLayout Layout
    {
        get => _layout;
        private set => _layout = value;
    }

    /// <summary>
    /// 加载布局。文件不存在时创建默认布局（包含一个示例沙漏）。
    /// </summary>
    public void LoadLayout()
    {
        try
        {
            if (File.Exists(LayoutPath))
            {
                Layout = ConfigureFileHelper.LoadConfig<Models.CeDesktopLayout>(LayoutPath);
            }
            else
            {
                Layout = new Models.CeDesktopLayout();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[CE] 加载桌面组件布局失败，使用默认布局");
            Layout = new Models.CeDesktopLayout();
        }
        // 空布局时放入示例沙漏（位置右上，避开主屏中央）
        if (Layout.Items.Count == 0)
        {
            var sample = new Models.CeDesktopItem
            {
                ComponentId = "88cc3bf3-98bd-4bf9-b3b2-c66e042c7b0b",
                X = 120,
                Y = 160,
                Width = 220,
                Height = 180
            };
            Layout.Items.Add(sample);
            SaveLayout();
        }
        Layout.Items.CollectionChanged += (_, _) => SaveLayout();
        foreach (var item in Layout.Items)
        {
            item.PropertyChanged += (_, _) => SaveLayout();
        }
    }

    /// <summary>保存布局到磁盘</summary>
    public void SaveLayout()
    {
        try
        {
            var dir = Path.GetDirectoryName(LayoutPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            ConfigureFileHelper.SaveConfig(LayoutPath, Layout);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[CE] 保存桌面组件布局失败");
        }
    }

    /// <summary>添加一个新的桌面组件并持久化</summary>
    public Models.CeDesktopItem AddItem(string componentId, double x, double y, double width = 220, double height = 160)
    {
        var item = new Models.CeDesktopItem
        {
            ComponentId = componentId,
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
        // 为容器类组件（如 Group/Stack）初始化设置 
        var info = ComponentRegistryService.Registered.FirstOrDefault(i =>
            string.Equals(i.Guid.ToString(), componentId, StringComparison.CurrentCultureIgnoreCase));
        if (info?.ComponentType?.BaseType?.IsGenericType == true)
        {
            var settingsType = info.ComponentType.BaseType.GetGenericArguments().FirstOrDefault();
            if (settingsType != null && settingsType.IsClass && !settingsType.IsAbstract)
            {
                try
                {
                    item.Settings = Activator.CreateInstance(settingsType);
                }
                catch (Exception)
                {
                    // 某些设置类型是 ObservableRecipient，可能缺少默认构造，忽略
                }
            }
        }
        Layout.Items.Add(item);
        SaveLayout();
        return item;
    }

    /// <summary>移除一个桌面组件并持久化</summary>
    public void RemoveItem(Models.CeDesktopItem item)
    {
        Layout.Items.Remove(item);
        SaveLayout();
    }

    /// <summary>
    /// 实例化某个布局项对应的组件视图
    /// </summary>
    public Core.Abstractions.Controls.ComponentBase? CreateComponentView(Models.CeDesktopItem item, bool isSettings = false)
    {
        if (string.IsNullOrWhiteSpace(item.ComponentId))
            return null;
        var cs = new ComponentSettings { Id = item.ComponentId, Settings = item.Settings };
        var component = ComponentsService.GetComponent(cs, isSettings);
        if (component != null && !isSettings)
        {
            item.Settings = cs.Settings;
        }
        return component;
    }
}