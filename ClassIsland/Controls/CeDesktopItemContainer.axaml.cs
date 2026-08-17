using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Components;
using ClassIsland.Core.Services.Registry;
using ClassIsland.Models;
using ClassIsland.Services;

namespace ClassIsland.Controls;

/// <summary>
/// 桌面组件容器（Classland CE）：承载一个组件实例，支持编辑模式下拖拽、缩放、删除、设置。
/// </summary>
public partial class CeDesktopItemContainer : UserControl
{
    public static readonly StyledProperty<CeDesktopItem?> ItemProperty =
        AvaloniaProperty.Register<CeDesktopItemContainer, CeDesktopItem?>(nameof(Item));

    /// <summary>关联的布局项</summary>
    public CeDesktopItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public static readonly StyledProperty<bool> IsEditModeProperty =
        AvaloniaProperty.Register<CeDesktopItemContainer, bool>(nameof(IsEditMode), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>编辑模式开关</summary>
    public bool IsEditMode
    {
        get => GetValue(IsEditModeProperty);
        set => SetValue(IsEditModeProperty, value);
    }

    private bool _dragging;
    private Point _dragStartPoint;
    private double _dragStartX, _dragStartY;
    private string _resizeDirection = "";
    private Point _resizeStartPoint;
    private double _resizeStartWidth, _resizeStartHeight, _resizeStartX, _resizeStartY;
    private CeDesktopLayoutService? _layoutService;

    public CeDesktopItemContainer()
    {
        InitializeComponent();
        RootGrid.PointerPressed += RootGrid_OnPointerPressed;
        RootGrid.PointerMoved += RootGrid_OnPointerMoved;
        RootGrid.PointerReleased += RootGrid_OnPointerReleased;
        RootGrid.PointerCaptureLost += RootGrid_OnPointerCaptureLost;
        RootGrid.DoubleTapped += RootGrid_OnDoubleTapped;

        // 缩放控制点事件
        ResizeHandleTL.PointerPressed += (s, e) => StartResize("TL", e);
        ResizeHandleTR.PointerPressed += (s, e) => StartResize("TR", e);
        ResizeHandleBL.PointerPressed += (s, e) => StartResize("BL", e);
        ResizeHandleBR.PointerPressed += (s, e) => StartResize("BR", e);
        ResizeHandleL.PointerPressed += (s, e) => StartResize("L", e);
        ResizeHandleR.PointerPressed += (s, e) => StartResize("R", e);
        ResizeHandleT.PointerPressed += (s, e) => StartResize("T", e);
        ResizeHandleB.PointerPressed += (s, e) => StartResize("B", e);

        // 顶部工具条拖动（编辑模式）
        EditToolbar.PointerPressed += (s, e) =>
        {
            if (!IsEditMode) return;
            BeginDrag(e);
            e.Handled = true;
        };

        ItemProperty.Changed.AddClassHandler<CeDesktopItemContainer>((o, e) => o.OnItemChanged(e));
        IsEditModeProperty.Changed.AddClassHandler<CeDesktopItemContainer>((o, e) => o.OnEditModeChanged(e));
        Loaded += (_, _) =>
        {
            if (Item != null) InitContent();
        };
    }

    private void OnItemChanged(Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (Item != null) InitContent();
    }

    private void OnEditModeChanged(Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        var isEdit = e.NewValue is true;
        EditBorder.IsVisible = isEdit;
        SetHandlesVisible(isEdit);
        // 编辑模式下组件内容不拦截点击（由容器统一处理）
        ContentHost.IsHitTestVisible = !isEdit;
        RootGrid.Cursor = isEdit ? new Cursor(StandardCursorType.SizeAll) : Cursor.Default;
        if (!isEdit)
        {
            _dragging = false;
            _resizeDirection = "";
        }
    }

    private void SetHandlesVisible(bool visible)
    {
        ResizeHandleTL.IsVisible = visible;
        ResizeHandleTR.IsVisible = visible;
        ResizeHandleBL.IsVisible = visible;
        ResizeHandleBR.IsVisible = visible;
        ResizeHandleL.IsVisible = visible;
        ResizeHandleR.IsVisible = visible;
        ResizeHandleT.IsVisible = visible;
        ResizeHandleB.IsVisible = visible;
    }

    /// <summary>初始化组件内容</summary>
    private void InitContent()
    {
        if (Item == null) return;
        _layoutService = App.GetService<CeDesktopLayoutService>();
        var component = _layoutService?.CreateComponentView(Item);
        if (component == null) return;

        ContentHost.Content = component;

        var info = ComponentRegistryService.Registered.FirstOrDefault(i =>
            string.Equals(i.Guid.ToString(), Item.ComponentId, StringComparison.CurrentCultureIgnoreCase));
        ItemNameText.Text = info?.Name ?? ComponentInfo.Empty.Name;

        // 让沙漏组件内容自适应容器尺寸
        component.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        component.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
    }

    #region 拖拽移动

    private void BeginDrag(PointerEventArgs e)
    {
        if (!IsEditMode || Item == null) return;
        var pos = e.GetPosition(RootGrid);
        _dragging = true;
        _dragStartPoint = pos;
        _dragStartX = Avalonia.Controls.Canvas.GetLeft(this);
        _dragStartY = Avalonia.Controls.Canvas.GetTop(this);
        RootGrid.Cursor = new Cursor(StandardCursorType.SizeAll);
        e.Handled = true;
    }

    private void RootGrid_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEditMode || Item == null) return;
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            ShowContextMenu();
            e.Handled = true;
            return;
        }
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        BeginDrag(e);
    }

    private void RootGrid_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsEditMode) return;
        if (_dragging && Item != null && Parent is Canvas canvas)
        {
            var pos = e.GetPosition(RootGrid);
            var dx = pos.X - _dragStartPoint.X;
            var dy = pos.Y - _dragStartPoint.Y;
            var newX = Math.Max(0, _dragStartX + dx);
            var newY = Math.Max(0, _dragStartY + dy);
            // 限制在画布内
            if (canvas.Bounds.Width > 0) newX = Math.Min(newX, canvas.Bounds.Width - Item.Width);
            if (canvas.Bounds.Height > 0) newY = Math.Min(newY, canvas.Bounds.Height - Item.Height);
            Canvas.SetLeft(this, newX);
            Canvas.SetTop(this, newY);
            Item.X = newX;
            Item.Y = newY;
        }
        else if (_resizeDirection != "" && Item != null)
        {
            Resize(e);
        }
    }

    private void RootGrid_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragging = false;
        _resizeDirection = "";
        if (Item != null) _layoutService?.SaveLayout();
    }

    private void RootGrid_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _dragging = false;
        _resizeDirection = "";
    }

    #endregion

    #region 缩放

    private void StartResize(string direction, PointerPressedEventArgs e)
    {
        if (!IsEditMode || Item == null) return;
        _resizeDirection = direction;
        _resizeStartPoint = e.GetPosition(this);
        _resizeStartWidth = Item.Width;
        _resizeStartHeight = Item.Height;
        _resizeStartX = Item.X;
        _resizeStartY = Item.Y;
        // 用捕获确保拖动期间继续收到事件
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void Resize(PointerEventArgs e)
    {
        if (Item == null) return;
        var pos = e.GetPosition(this);
        var dx = pos.X - _resizeStartPoint.X;
        var dy = pos.Y - _resizeStartPoint.Y;
        const double minW = 60, minH = 48;

        var newX = _resizeStartX;
        var newY = _resizeStartY;
        var newW = _resizeStartWidth;
        var newH = _resizeStartHeight;

        if (_resizeDirection.Contains("R")) newW = Math.Max(minW, _resizeStartWidth + dx);
        if (_resizeDirection.Contains("B")) newH = Math.Max(minH, _resizeStartHeight + dy);
        if (_resizeDirection.Contains("L"))
        {
            newW = Math.Max(minW, _resizeStartWidth - dx);
            newX = _resizeStartX + (_resizeStartWidth - newW);
        }
        if (_resizeDirection.Contains("T"))
        {
            newH = Math.Max(minH, _resizeStartHeight - dy);
            newY = _resizeStartY + (_resizeStartHeight - newH);
        }

        Item.Width = newW;
        Item.Height = newH;
        Item.X = newX;
        Item.Y = newY;
    }

    #endregion

    #region 操作

    private void RootGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (IsEditMode && Item != null)
        {
            OpenSettings();
            e.Handled = true;
        }
    }

    private void SettingsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => OpenSettings();

    private void DeleteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Item == null) return;
        _layoutService?.RemoveItem(Item);
        e.Handled = true;
    }

    private void OpenSettings()
    {
        if (Item == null || _layoutService == null) return;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var window = new Views.CeComponentSettingsWindow(_layoutService, Item)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                var owner = this.GetVisualRoot() as Window;
                if (owner != null) window.ShowDialog(owner);
                else window.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CE] 打开组件设置失败: {ex.Message}");
            }
        });
    }

    private void ShowContextMenu()
    {
        if (Item == null) return;
        var menu = new ContextMenu();
        var editItem = new MenuItem { Header = IsEditMode ? "退出编辑" : "进入编辑" };
        editItem.Click += (_, _) => IsEditMode = !IsEditMode;
        var settingsItem = new MenuItem { Header = "组件设置" };
        settingsItem.Click += (_, _) => OpenSettings();
        var deleteItem = new MenuItem { Header = "删除组件" };
        deleteItem.Click += (_, _) => _layoutService?.RemoveItem(Item);
        menu.Items.Add(editItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(deleteItem);
        Dispatcher.UIThread.Post(() =>
        {
            menu.Open(this);
        });
    }

    #endregion
}