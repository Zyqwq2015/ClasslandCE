using System;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Services.Registry;
using ClassIsland.Models;
using ClassIsland.Services;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Views;

/// <summary>
/// 桌面组件窗口（Classland CE）：全屏透明置底窗口，承载壁纸上的浮动组件。
/// <para>编辑模式下支持拖拽/缩放/删除/添加；非编辑模式组件可交互点击。</para>
/// </summary>
public partial class CeDesktopWindow : Window
{
    public static readonly string HourglassComponentId = "88CC3BF3-98BD-4BF9-B3B2-C66E042C7B0B";
    public static readonly string LessonShortcutComponentId = "D80EBE4A-9F35-4D71-9184-FF3EA0926A2C";

    private CeDesktopLayoutService? _layoutService;
    private SettingsService? _settingsService;
    private bool _isEditMode;
    private bool _isClickSelecting;
    private Avalonia.Point _mouseDownPoint;

    #region Win32 - 挂到桌面（WorkerW）

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_BOTTOM = new(1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOOWNERZORDER = 0x0200;
    private const uint SWP_NOSENDCHANGING = 0x0400;
    private const uint SWP_NOREPOSITION = 0x0200;
    private const uint SWP_FRAMECHANGED = 0x0020;

    #endregion

    public CeDesktopWindow()
    {
        InitializeComponent();
        _layoutService = App.GetService<CeDesktopLayoutService>();
        _settingsService = App.GetService<SettingsService>();
        _settingsService!.Settings.PropertyChanged += SettingsOnPropertyChanged;

        // 全屏透明窗口：构造函数就设好尺寸，避免首帧闪烁
        var screen = Screens.Primary;
        if (screen != null)
        {
            Width = screen.Bounds.Width;
            Height = screen.Bounds.Height;
            Position = screen.Bounds.Position;
        }
        else
        {
            Width = 1920;
            Height = 1080;
        }
        Opacity = 1.0;

        Loaded += CeDesktopWindow_OnLoaded;
        SizeChanged += (_, _) => PositionEditToolbar();
        Closed += (_, _) => _settingsService.Settings.PropertyChanged -= SettingsOnPropertyChanged;
    }

    private void SettingsOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsService.Settings.CeIsDesktopWidgetsEnabled):
                if (_settingsService!.Settings.CeIsDesktopWidgetsEnabled) ShowDesktopWindow();
                else HideDesktopWindow();
                break;
            case nameof(SettingsService.Settings.CeIsDesktopWidgetsEditMode):
                SetEditMode(_settingsService!.Settings.CeIsDesktopWidgetsEditMode);
                break;
        }
    }

    private void CeDesktopWindow_OnLoaded(object? sender, EventArgs e)
    {
        // 尺寸在构造函数已设；这里再按当前主屏校验一次
        var screen = Screens.Primary;
        if (screen != null)
        {
            Width = screen.Bounds.Width;
            Height = screen.Bounds.Height;
            Position = screen.Bounds.Position;
        }
        // 挂到桌面层（WorkerW）
        if (OperatingSystem.IsWindows())
        {
            Dispatcher.UIThread.Post(() => AttachToDesktop(), DispatcherPriority.Background);
        }
        // 加载布局
        _layoutService?.LoadLayout();
        RepopulateCanvas();
        // 应用编辑模式
        SetEditMode(_settingsService?.Settings.CeIsDesktopWidgetsEditMode ?? false);
        // 应用是否显示
        if (_settingsService?.Settings.CeIsDesktopWidgetsEnabled == true)
            ShowDesktopWindow();
    }

    #region 桌面挂载

    private void AttachToDesktop()
    {
        try
        {
            var handle = TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (handle == nint.Zero) return;
            // 找到 Progman -> WorkerW
            var progman = FindWindow("Progman", null);
            if (progman == nint.Zero) return;
            // 发送消息让系统创建 WorkerW（经典做法）
            SendMessage(progman, 0x052C, new IntPtr(0xD), IntPtr.Zero);
            // 轮询寻找 WorkerW
            nint workerW = nint.Zero;
            for (var i = 0; i < 20; i++)
            {
                workerW = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "WorkerW", null);
                if (workerW != nint.Zero && workerW != progman) break;
                System.Threading.Thread.Sleep(50);
            }
            if (workerW == nint.Zero) return;

            SetParent(handle, workerW);
            SetWindowPos(handle, HWND_BOTTOM, 0, 0, (int)Width, (int)Height, SWP_NOACTIVATE);
            Logger.LogInformation("[CE] 桌面组件窗口已挂载到 WorkerW");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[CE] 桌面组件窗口挂载失败，退回普通置顶模式");
            Topmost = true;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private ILogger<CeDesktopWindow>? _logger;

    private ILogger<CeDesktopWindow> Logger => _logger ??= (App.GetService<ILogger<CeDesktopWindow>>()
        ?? throw new InvalidOperationException("无法获取 ILogger<CeDesktopWindow>"));

    #endregion

    #region 布局

    private void RepopulateCanvas()
    {
        WidgetsCanvas.Children.Clear();
        if (_layoutService == null) return;
        foreach (var item in _layoutService.Layout.Items)
        {
            var container = new Controls.CeDesktopItemContainer
            {
                Item = item,
                IsEditMode = _isEditMode
            };
            container.Width = item.Width;
            container.Height = item.Height;
            Canvas.SetLeft(container, item.X);
            Canvas.SetTop(container, item.Y);
            WidgetsCanvas.Children.Add(container);
        }
        PositionEditToolbar();
    }

    private void PositionEditToolbar()
    {
        var canvasH = WidgetsCanvas.Bounds.Height > 0 ? WidgetsCanvas.Bounds.Height : Height;
        var toolbarH = EditToolbarPanel.Bounds.Height > 0 ? EditToolbarPanel.Bounds.Height : 44;
        if (canvasH <= 0) return;
        Canvas.SetLeft(EditToolbarPanel, 12);
        Canvas.SetTop(EditToolbarPanel, canvasH - toolbarH - 16);
    }

    public void SetEditMode(bool edit)
    {
        _isEditMode = edit;
        EditToolbarPanel.IsVisible = edit;
        foreach (var child in WidgetsCanvas.Children.OfType<Controls.CeDesktopItemContainer>())
        {
            child.IsEditMode = edit;
        }
    }

    private void ShowDesktopWindow()
    {
        if (!IsVisible)
        {
            Show();
        }
        // 更新所有子组件尺寸
        foreach (var item in _layoutService?.Layout.Items ?? [])
        {
            var container = WidgetsCanvas.Children.OfType<Controls.CeDesktopItemContainer>()
                .FirstOrDefault(c => c.Item == item);
            if (container != null)
            {
                container.Width = item.Width;
                container.Height = item.Height;
                Canvas.SetLeft(container, item.X);
                Canvas.SetTop(container, item.Y);
            }
        }
        PositionEditToolbar();
    }

    private void HideDesktopWindow()
    {
        Hide();
    }

    private void Canvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isEditMode) return;
        _isClickSelecting = true;
        _mouseDownPoint = e.GetPosition(WidgetsCanvas);
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            ShowAddMenu(e.GetPosition(WidgetsCanvas));
            e.Handled = true;
        }
    }

    private void Canvas_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isEditMode) return;
        // 空画布右键拖拽选择框（预留）
    }

    private void Canvas_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isEditMode) return;
        _isClickSelecting = false;
    }

    #endregion

    #region 添加组件

    private void AddComponentToCanvas(string componentId, double x, double y)
    {
        if (_layoutService == null) return;
        _layoutService.AddItem(componentId, x, y);
        // 重绘
        RepopulateCanvas();
    }

    private void AddHourglassButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddComponentAtRandomPos(HourglassComponentId);
    }

    private void AddShortcutButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddComponentAtRandomPos(LessonShortcutComponentId);
    }

    private void AddPomodoroButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 番茄时钟组件 ID
        AddComponentAtRandomPos(FindComponentIdByName("番茄时钟"));
    }

    private void AddClockButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddComponentAtRandomPos(FindComponentIdByName("时钟"));
    }

    private string FindComponentIdByName(string name)
    {
        return ComponentRegistryService.Registered.FirstOrDefault(i => i.Name.Contains(name))?.Guid.ToString() ?? "";
    }

    private void AddComponentAtRandomPos(string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId)) return;
        var w = WidgetsCanvas.Bounds.Width > 0 ? WidgetsCanvas.Bounds.Width : 800;
        var h = WidgetsCanvas.Bounds.Height > 0 ? WidgetsCanvas.Bounds.Height : 600;
        var x = Math.Max(0, (w - 220) / 2 + new Random().Next(-100, 100));
        var y = Math.Max(0, (h - 160) / 2 + new Random().Next(-60, 60));
        AddComponentToCanvas(componentId, x, y);
    }

    private void ShowAddMenu(Avalonia.Point pos)
    {
        var menu = new ContextMenu();
        var addHourglass = new MenuItem { Header = "添加 时间沙漏" };
        addHourglass.Click += (_, _) => AddComponentToCanvas(HourglassComponentId, pos.X, pos.Y);
        var addShortcut = new MenuItem { Header = "添加 课件快捷方式" };
        addShortcut.Click += (_, _) => AddComponentToCanvas(LessonShortcutComponentId, pos.X, pos.Y);
        menu.Items.Add(addHourglass);
        menu.Items.Add(addShortcut);
        var addPomodoro = new MenuItem { Header = "添加 番茄时钟" };
        var pomodoroId = FindComponentIdByName("番茄时钟");
        addPomodoro.Click += (_, _) => AddComponentToCanvas(pomodoroId, pos.X, pos.Y);
        menu.Items.Add(addPomodoro);
        var addClock = new MenuItem { Header = "添加 时钟" };
        var clockId = FindComponentIdByName("时钟");
        addClock.Click += (_, _) => AddComponentToCanvas(clockId, pos.X, pos.Y);
        menu.Items.Add(addClock);
        menu.Items.Add(new Separator());
        var exitEdit = new MenuItem { Header = "退出编辑模式" };
        exitEdit.Click += (_, _) => ExitEditMode();
        menu.Items.Add(exitEdit);
        menu.Open(this);
    }

    private void ExitEditModeButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ExitEditMode();
    }

    private void ExitEditMode()
    {
        if (_settingsService != null)
            _settingsService.Settings.CeIsDesktopWidgetsEditMode = false;
        SetEditMode(false);
    }

    #endregion
}