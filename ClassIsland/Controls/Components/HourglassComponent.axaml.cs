using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 时间沙漏组件：带 3D 立体玻璃质感的可视化倒计时沙漏动画
/// </summary>
[ComponentInfo("88CC3BF3-98BD-4BF9-B3B2-C66E042C7B0B", "时间沙漏", "\uE823", "3D 立体玻璃沙漏倒计时，可暂停、重置、自动循环。")]
public partial class HourglassComponent : ComponentBase<HourglassComponentSettings>
{
    private DispatcherTimer? _timer;
    private double _remaining;
    private bool _running;

    // 沙漏几何参数（画布 180x150）
    private const double FrameY = 6;              // 木框厚度方向
    private const double TopInnerY = 22;          // 上瓶玻璃起点（木框内）
    private const double BottomInnerY = 128;      // 下瓶玻璃终点
    private const double MidY = 75;               // 沙漏腰
    private const double HalfTopW = 66;           // 上瓶半径
    private const double HalfWaist = 9;           // 腰半径

    public HourglassComponent()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _remaining = Settings.RemainingSeconds > 0 ? Settings.RemainingSeconds : Settings.DurationSeconds;
        _running = Settings.Started;
        UpdateButtonText();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => Tick();
        if (_running) _timer.Start();
        Render();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
        Settings.RemainingSeconds = Math.Max(0, _remaining);
        Settings.Started = _running;
    }

    private void Tick()
    {
        if (!_running) return;
        _remaining -= 0.05;
        if (_remaining <= 0)
        {
            _remaining = 0;
            if (Settings.AutoRestart)
            {
                _remaining = Settings.DurationSeconds;
            }
            else
            {
                _running = false;
                _timer?.Stop();
                UpdateButtonText();
            }
        }
        Render();
    }

    private double Progress => _remaining <= 0 ? 0.0 : Math.Clamp(_remaining / Settings.DurationSeconds, 0.0, 1.0);

    /// <summary>
    /// 计算沙漏玻璃瓶的轮廓（左壁和右壁），用于裁剪沙子形状。
    /// </summary>
    private static void BottleProfile(double halfTop, double halfWaist, double topY, double waistY, double bottomY, out (double y, double half)[] left, out (double y, double half)[] right)
    {
        left = new (double, double)[]
        {
            (topY, halfTop),
            (waistY, halfWaist),
            (bottomY, halfTop * 0.96)
        };
        right = new (double, double)[]
        {
            (topY, -halfTop),
            (waistY, -halfWaist),
            (bottomY, -halfTop * 0.96)
        };
    }

    private double WallHalf(double y) => HalfTopW - (HalfTopW - HalfWaist) * Math.Clamp((y - TopInnerY) / (BottomInnerY - TopInnerY), 0, 1);

    private void Render()
    {
        var p = Progress;
        var cx = 90.0;

        // 沙子总量占上下两个瓶子的比例（视觉上更接近真实沙漏）
        // 上瓶沙高随剩余时间线性下降；下瓶沙同量上升
        var sandTopFill = 1.0 - p;          // 上瓶剩余比例
        var sandBottomFill = p;             // 下瓶已落比例

        // 上瓶沙子表面 y 坐标（从瓶颈顶部开始下降）
        var topSandSurfaceY = TopInnerY + (MidY - TopInnerY) * sandTopFill * 0.92;
        // 下瓶沙子表面 y 坐标（从腰往上堆：表面从 BottomInnerY 升到腰之下） 
        var bottomSandSurfaceY = BottomInnerY - (BottomInnerY - MidY) * sandBottomFill * 0.92;

        // ---- 阴影（椭圆，位于底部） ----
        ShadowPath.Data = StreamGeometry.Parse(
            $"M {cx - 52} 144 C {cx - 52} 148, {cx + 52} 148, {cx + 52} 144 C {cx + 52} 140, {cx - 52} 140, {cx - 52} 144 Z");
        ShadowPath.Fill = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));

        // ---- 玻璃主体（半透明淡蓝，模拟玻璃） ----
        var glass = $@"M {cx - HalfTopW} {TopInnerY} L {cx + HalfTopW} {TopInnerY}
            C {cx + HalfTopW + 6} {TopInnerY + 30}, {cx + HalfWaist + 2} {MidY - 8}, {cx + HalfWaist} {MidY}
            C {cx + HalfWaist - 2} {MidY + 8}, {cx + HalfTopW + 6} {BottomInnerY - 30}, {cx + HalfTopW} {BottomInnerY}
            L {cx - HalfTopW} {BottomInnerY}
            C {cx - HalfTopW - 6} {BottomInnerY - 30}, {cx - HalfWaist - 2} {MidY + 8}, {cx - HalfWaist} {MidY}
            C {cx - HalfWaist + 2} {MidY - 8}, {cx - HalfTopW - 6} {TopInnerY + 30}, {cx - HalfTopW} {TopInnerY} Z";
        GlassBody.Data = StreamGeometry.Parse(glass);
        GlassBody.Fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(90, 160, 210, 255), 0),
                new GradientStop(Color.FromArgb(35, 220, 240, 255), 0.5),
                new GradientStop(Color.FromArgb(90, 120, 180, 240), 1)
            }
        };

        // ---- 玻璃左侧高光（弧形亮带） ----
        var highlight = $@"M {cx - HalfTopW + 6} {TopInnerY + 12}
            C {cx - HalfTopW + 5} {MidY - 6}, {cx - HalfWaist - 1} {MidY}, {cx - HalfTopW + 2} {BottomInnerY - 12}
            L {cx - HalfTopW + 9} {BottomInnerY - 12}
            C {cx - HalfTopW + 8} {MidY + 4}, {cx - HalfWaist + 3} {MidY - 6}, {cx - HalfTopW + 12} {TopInnerY + 12} Z";
        GlassHighlight.Data = StreamGeometry.Parse(highlight);
        GlassHighlight.Fill = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));

        // ---- 沙子：上瓶（从瓶颈向下，宽度按瓶身轮廓） ----
        // 上瓶沙子是一个梯形：顶边 y = topSandSurfaceY，宽度 = WallHalf(topSandSurfaceY)*2 - 2
        var topHalf = Math.Max(6, WallHalf(topSandSurfaceY) - 2);
        var sandTop = $@"M {cx - topHalf} {topSandSurfaceY} L {cx + topHalf} {topSandSurfaceY}
            C {cx + topHalf - 4} {MidY - 2}, {cx + HalfWaist - 1} {MidY - 1}, {cx + HalfWaist - 1} {MidY}
            L {cx - HalfWaist + 1} {MidY} C {cx - HalfWaist + 1} {MidY - 1}, {cx - topHalf + 4} {MidY - 2}, {cx - topHalf} {topSandSurfaceY} Z";
        SandTop.Data = StreamGeometry.Parse(sandTop);
        SandTop.Fill = SandBrush();

        // ---- 沙子：下瓶（锥形堆，表面随沙量上升） ----
        var botHalf = Math.Max(5, WallHalf(bottomSandSurfaceY) - 3);
        var sandBottom = $@"M {cx - botHalf} {bottomSandSurfaceY} L {cx + botHalf} {bottomSandSurfaceY}
            C {cx + botHalf - 4} {BottomInnerY - 10}, {cx + HalfWaist - 1} {BottomInnerY - 14}, {cx + HalfWaist - 2} {BottomInnerY - 4}
            L {cx - HalfWaist + 2} {BottomInnerY - 4} C {cx - HalfWaist + 1} {BottomInnerY - 14}, {cx - botHalf + 4} {BottomInnerY - 10}, {cx - botHalf} {bottomSandSurfaceY} Z";
        // 让底部贴瓶底
        sandBottom = $@"M {cx - botHalf} {bottomSandSurfaceY} L {cx + botHalf} {bottomSandSurfaceY}
            C {cx + botHalf - 6} {BottomInnerY - 6}, {cx + HalfWaist - 2} {BottomInnerY - 14}, {cx + HalfWaist} {BottomInnerY - 4}
            L {cx - HalfWaist} {BottomInnerY - 4} C {cx - HalfWaist + 2} {BottomInnerY - 14}, {cx - botHalf + 6} {BottomInnerY - 6}, {cx - botHalf} {bottomSandSurfaceY} Z";
        SandBottom.Data = StreamGeometry.Parse(sandBottom);
        SandBottom.Fill = SandBrush();

        // ---- 沙子表面高光（一条亮线） ----
        var shineY = (topSandSurfaceY + bottomSandSurfaceY) / 2;
        SandShine.Data = null;

        // ---- 顶部/底部木框（带立体厚度） ----
        TopFrame.Data = StreamGeometry.Parse(
            $@"M {cx - 76} {FrameY} L {cx + 76} {FrameY} L {cx + 76} {TopInnerY} L {cx - 76} {TopInnerY} Z");
        TopFrame.Fill = WoodBrush();
        BottomFrame.Data = StreamGeometry.Parse(
            $@"M {cx - 76} {BottomInnerY} L {cx + 76} {BottomInnerY} L {cx + 76} {BottomInnerY + 16} L {cx - 76} {BottomInnerY + 16} Z");
        BottomFrame.Fill = WoodBrush();

        // ---- 玻璃描边（强化轮廓） ----
        GlassOutline.Data = StreamGeometry.Parse(glass);
        GlassOutline.Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
        GlassOutline.StrokeThickness = 1.5;
        GlassOutline.Fill = null;

        // 时间文本
        TimeText.Text = FormatTime(Math.Max(0, _remaining));
    }

    private IBrush SandBrush()
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xE0, 0xB9, 0x6E), 0),
                new GradientStop(Color.FromRgb(0xC9, 0x9A, 0x4E), 0.5),
                new GradientStop(Color.FromRgb(0xF2, 0xD6, 0x9B), 1)
            }
        };
    }

    private IBrush WoodBrush()
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x8B, 0x5A, 0x2B), 0),
                new GradientStop(Color.FromRgb(0xA0, 0x6B, 0x35), 0.5),
                new GradientStop(Color.FromRgb(0x6E, 0x44, 0x1E), 1)
            }
        };
    }

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Ceiling(seconds));
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
    }

    private void ToggleButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_running)
        {
            _running = false;
            _timer?.Stop();
            Settings.Started = false;
        }
        else
        {
            if (_remaining <= 0) _remaining = Settings.DurationSeconds;
            _running = true;
            Settings.Started = true;
            _timer?.Start();
        }
        UpdateButtonText();
    }

    private void ResetButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _running = false;
        _timer?.Stop();
        _remaining = Settings.DurationSeconds;
        Settings.Started = false;
        Settings.RemainingSeconds = Settings.DurationSeconds;
        UpdateButtonText();
        Render();
    }

    private void UpdateButtonText()
    {
        if (ToggleButton != null)
            ToggleButton.Content = _running ? "暂停" : "开始";
    }
}