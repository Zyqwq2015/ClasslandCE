using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 精致优雅的 3D 玻璃沙漏渲染器（自绘高性能版）
/// 所有画刷/画笔/静态几何只构建一次并复用；每帧仅重建少量轻量动态几何，
/// 不解析字符串路径、不在 UI 线程创建新画刷、时间文本仅在整秒变化时更新 ——
/// 彻底规避原实现每 50ms 疯狂分配导致的界面卡死。
/// </summary>
public class HourglassRenderer : Control
{
    // ============ 画布几何参数（画布 200x200） ============
    private const double Cx = 100;            // 中心 X
    private const double TopY = 34;           // 上瓶玻璃顶
    private const double WaistY = 100;        // 腰部
    private const double BottomY = 166;       // 下瓶玻璃底
    private const double HalfTop = 56;        // 上瓶半径
    private const double HalfWaist = 10;      // 腰半径
    private const double FrameR = 64;         // 黄铜框架半径
    private const double FrameThick = 6;      // 框架厚度

    private double _remaining = 60;
    private double _duration = 60;
    private int _lastShownSecond = -1;
    private string _lastText = "";

    public double Remaining
    {
        get => _remaining;
        set { _remaining = value; InvalidateVisual(); }
    }

    public double Duration
    {
        get => _duration;
        set { _duration = value > 0 ? value : 1; InvalidateVisual(); }
    }

    // ============ 静态画刷（进程内只建一次） ============
    private static readonly IBrush GlassBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(64, 208, 234, 252), 0),
            new GradientStop(Color.FromArgb(10, 226, 246, 255), 0.45),
            new GradientStop(Color.FromArgb(48, 138, 184, 236), 0.8),
            new GradientStop(Color.FromArgb(100, 84, 140, 200), 1)
        }
    };

    private static readonly IBrush GlassInnerBrush = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255));

    private static readonly IBrush GlassEdgeBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(150, 255, 255, 255), 0),
            new GradientStop(Color.FromArgb(30, 255, 255, 255), 0.5),
            new GradientStop(Color.FromArgb(140, 170, 210, 250), 1)
        }
    };

    private static readonly IBrush SandBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(0xF6, 0xD5, 0x93), 0),
            new GradientStop(Color.FromRgb(0xE2, 0xB3, 0x66), 0.35),
            new GradientStop(Color.FromRgb(0xCA, 0x92, 0x42), 0.7),
            new GradientStop(Color.FromRgb(0xA6, 0x6E, 0x2C), 1)
        }
    };

    private static readonly IBrush SandShineBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
            new GradientStop(Color.FromArgb(180, 255, 249, 225), 0.5),
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
        }
    };

    private static readonly IBrush BrassBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(0xF4, 0xDF, 0xAE), 0),
            new GradientStop(Color.FromRgb(0xC9, 0x9B, 0x5C), 0.5),
            new GradientStop(Color.FromRgb(0x84, 0x59, 0x28), 1)
        }
    };

    private static readonly IBrush BrassRingBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(0x8A, 0x5E, 0x2C), 0),
            new GradientStop(Color.FromRgb(0xE8, 0xC8, 0x8A), 0.5),
            new GradientStop(Color.FromRgb(0x6E, 0x49, 0x20), 1)
        }
    };

    private static readonly IBrush TimeBrush = new SolidColorBrush(Color.FromArgb(185, 255, 255, 255));
    private static readonly Typeface TimeFace = new("Microsoft YaHei UI", FontStyle.Normal, FontWeight.Light);

    // ============ 静态画笔 ============
    private static readonly IPen GlassEdgePen = new Pen(GlassEdgeBrush, 1.3);
    private static readonly IPen BrassOuterPen = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 228, 175)), 1.5);
    private static readonly IPen WaistRingPen = new Pen(BrassRingBrush, 2.0);
    private static readonly IPen EdgeHighlightPen = new Pen(new SolidColorBrush(Color.FromArgb(95, 255, 255, 255)), 1.5);
    private static readonly IPen SandShinePen = new Pen(SandShineBrush, 1.2);
    private static readonly IPen LeftHighlightPen = new Pen(
        new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(140, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(25, 255, 255, 255), 1)
            }
        }, 3.4)
    { LineCap = PenLineCap.Round };

    private static readonly IBrush ShadowBrush = new SolidColorBrush(Color.FromArgb(42, 0, 0, 0));
    private static readonly IBrush ShadowDeepBrush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));

    // ============ 静态几何（懒构建一次，永不重建） ============
    private StreamGeometry? _glassOuter;
    private StreamGeometry? _glassInner;

    private double Progress => _remaining <= 0
        ? 0
        : Math.Clamp(_remaining / (_duration > 0 ? _duration : 1), 0, 1);

    private static double WallHalf(double y) =>
        HalfTop - (HalfTop - HalfWaist) * Math.Clamp((y - TopY) / (BottomY - TopY), 0, 1);

    public HourglassRenderer()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            _glassOuter = null;
            _glassInner = null;
            InvalidateVisual();
        }
    }

    private StreamGeometry BuildGlassOuter()
    {
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(new Point(Cx - HalfTop, TopY), true);
        ctx.CubicBezierTo(
            new Point(Cx - HalfTop - 9, TopY + 28),
            new Point(Cx - HalfWaist - 7, WaistY - 11),
            new Point(Cx - HalfWaist, WaistY));
        ctx.CubicBezierTo(
            new Point(Cx - HalfWaist + 2, WaistY + 9),
            new Point(Cx - HalfTop - 5, BottomY - 28),
            new Point(Cx - HalfTop + 2, BottomY));
        ctx.LineTo(new Point(Cx + HalfTop - 2, BottomY));
        ctx.CubicBezierTo(
            new Point(Cx + HalfTop + 5, BottomY - 28),
            new Point(Cx + HalfWaist - 2, WaistY + 9),
            new Point(Cx + HalfWaist, WaistY));
        ctx.CubicBezierTo(
            new Point(Cx + HalfWaist + 7, WaistY - 11),
            new Point(Cx + HalfTop + 9, TopY + 28),
            new Point(Cx + HalfTop, TopY));
        return g;
    }

    private StreamGeometry BuildGlassInner()
    {
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(new Point(Cx - HalfTop + 7, TopY + 6), true);
        ctx.CubicBezierTo(
            new Point(Cx - HalfTop + 5, TopY + 25),
            new Point(Cx - HalfWaist - 4, WaistY - 8),
            new Point(Cx - HalfWaist + 3, WaistY));
        ctx.CubicBezierTo(
            new Point(Cx - HalfWaist + 1, WaistY + 6),
            new Point(Cx - HalfTop + 5, BottomY - 25),
            new Point(Cx - HalfTop + 9, BottomY - 5));
        ctx.LineTo(new Point(Cx + HalfTop - 7, BottomY - 5));
        ctx.CubicBezierTo(
            new Point(Cx + HalfTop - 4, BottomY - 25),
            new Point(Cx + HalfWaist - 1, WaistY + 6),
            new Point(Cx + HalfWaist - 2, WaistY));
        ctx.CubicBezierTo(
            new Point(Cx + HalfWaist + 5, WaistY - 8),
            new Point(Cx + HalfTop - 4, TopY + 25),
            new Point(Cx + HalfTop - 7, TopY + 6));
        return g;
    }

    private double TopSandSurfaceY(double p)
    {
        var fill = 1 - p;
        return TopY + 9 + (WaistY - 14 - TopY - 9) * fill;
    }

    private double BottomSandSurfaceY(double p)
    {
        var fill = p;
        return BottomY - 9 - (BottomY - 14 - WaistY + 3) * fill;
    }

    public override void Render(DrawingContext context)
    {
        var p = Progress;

        _glassOuter ??= BuildGlassOuter();
        _glassInner ??= BuildGlassInner();

        // 1. 底部柔和投影
        context.DrawEllipse(ShadowDeepBrush, null, new Rect(Cx - 60, BottomY + FrameThick + 4, 120, 14));
        context.DrawEllipse(ShadowBrush, null, new Rect(Cx - 52, BottomY + FrameThick + 9, 104, 8));

        // 2. 底部黄铜圆盘框架
        var frameBottomRect = new Rect(Cx - FrameR, BottomY - 2, FrameR * 2, (FrameThick + 4) * 1.6);
        context.DrawEllipse(BrassBrush, BrassOuterPen, frameBottomRect);
        context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(90, 60, 40, 18)), 1.2),
            new Rect(Cx - FrameR + 6, BottomY - 2 + 3, (FrameR - 6) * 2, (FrameThick + 4) * 1.6 - 6));

        // 3. 沙子（先画沙，玻璃后覆盖营造"玻璃包沙"感）
        DrawBottomSand(context, p);
        DrawTopSand(context, p);

        // 4. 玻璃瓶身（半透明渐变 + 白色描边）
        context.DrawGeometry(GlassBrush, GlassEdgePen, _glassOuter!);
        context.DrawGeometry(GlassInnerBrush, null, _glassInner!);

        // 5. 左侧高光弧带
        var hl = new StreamGeometry();
        using (var hlc = hl.Open())
        {
            hlc.BeginFigure(new Point(Cx - HalfTop + 6, TopY + 11), false);
            hlc.CubicBezierTo(
                new Point(Cx - HalfTop + 3, WaistY - 13),
                new Point(Cx - HalfWaist - 1, WaistY - 2),
                new Point(Cx - HalfTop + 2, BottomY - 9));
            hlc.EndFigure(false);
        }
        context.DrawGeometry(null, LeftHighlightPen, hl);

        // 6. 顶部黄铜圆盘框架
        var frameTopRect = new Rect(Cx - FrameR, TopY - FrameThick - 6, FrameR * 2, (FrameThick + 4) * 1.6);
        context.DrawEllipse(BrassBrush, BrassOuterPen, frameTopRect);
        context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(90, 60, 40, 18)), 1.2),
            new Rect(Cx - FrameR + 6, TopY - FrameThick - 6 + 3, (FrameR - 6) * 2, (FrameThick + 4) * 1.6 - 6));

        // 7. 腰部金属箍环
        context.DrawLine(WaistRingPen,
            new Point(Cx - HalfWaist - 5, WaistY),
            new Point(Cx + HalfWaist + 5, WaistY));
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 255, 232, 185)), null,
            new Rect(Cx - HalfWaist - 4, WaistY - 3, (HalfWaist + 4) * 2, 6));

        // 8. 落沙细流（腰部上下连贯的细沙柱）
        DrawSandStream(context, p);

        // 9. 玻璃右侧边缘反光
        var er = new StreamGeometry();
        using (var erc = er.Open())
        {
            erc.BeginFigure(new Point(Cx + HalfTop - 5, TopY + 15), false);
            erc.CubicBezierTo(
                new Point(Cx + HalfTop - 2, WaistY - 9),
                new Point(Cx + HalfWaist + 3, WaistY),
                new Point(Cx + HalfTop - 6, BottomY - 12));
            erc.EndFigure(false);
        }
        context.DrawGeometry(null, EdgeHighlightPen, er);

        // 10. 时间文本（整秒才重绘文本，避免无谓重排）
        var whole = (int)Math.Ceiling(Math.Max(0, _remaining));
        if (whole != _lastShownSecond)
        {
            _lastShownSecond = whole;
            var ts = TimeSpan.FromSeconds(whole);
            _lastText = ts.TotalHours >= 1
                ? ts.ToString(@"h\:mm\:ss")
                : ts.ToString(@"m\:ss");
        }
        var ft = new FormattedText(_lastText, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, TimeFace, 13.5, TimeBrush);
        context.DrawText(ft, new Point(Cx - ft.Width / 2, WaistY - 9));
    }

    private void DrawTopSand(DrawingContext context, double p)
    {
        var surfaceY = TopSandSurfaceY(p);
        var half = Math.Max(4, WallHalf(surfaceY) - 4);
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(new Point(Cx - half, surfaceY), true);
        ctx.LineTo(new Point(Cx + half, surfaceY));
        ctx.CubicBezierTo(
            new Point(Cx + half - 8, WaistY - 3),
            new Point(Cx + HalfWaist - 1, WaistY - 4),
            new Point(Cx + HalfWaist - 1, WaistY - 2));
        ctx.LineTo(new Point(Cx - HalfWaist + 1, WaistY - 2));
        ctx.CubicBezierTo(
            new Point(Cx - HalfWaist + 1, WaistY - 4),
            new Point(Cx - half + 8, WaistY - 3),
            new Point(Cx - half, surfaceY));
        context.DrawGeometry(SandBrush, null, g);

        // 沙面高光细线
        var shine = new StreamGeometry();
        using (var sctx = shine.Open())
        {
            sctx.BeginFigure(new Point(Cx - half + 5, surfaceY + 1), false);
            sctx.LineTo(new Point(Cx + half - 5, surfaceY + 1));
            sctx.EndFigure(false);
        }
        context.DrawGeometry(null, SandShinePen, shine);
    }

    private void DrawBottomSand(DrawingContext context, double p)
    {
        var surfaceY = BottomSandSurfaceY(p);
        var half = Math.Max(5, WallHalf(surfaceY) - 4);
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(new Point(Cx - half, surfaceY), true);
        ctx.LineTo(new Point(Cx + half, surfaceY));
        ctx.CubicBezierTo(
            new Point(Cx + half - 6, BottomY - 8),
            new Point(Cx + HalfWaist, BottomY - 13),
            new Point(Cx + HalfWaist, BottomY - 7));
        ctx.LineTo(new Point(Cx - HalfWaist, BottomY - 7));
        ctx.CubicBezierTo(
            new Point(Cx - HalfWaist, BottomY - 13),
            new Point(Cx - half + 6, BottomY - 8),
            new Point(Cx - half, surfaceY));
        context.DrawGeometry(SandBrush, null, g);

        // 沙面高光（微微下凹的弧线）
        var shine = new StreamGeometry();
        using (var sctx = shine.Open())
        {
            sctx.BeginFigure(new Point(Cx - half + 6, surfaceY + 3), false);
            sctx.CubicBezierTo(
                new Point(Cx - half + 20, surfaceY + 0.5),
                new Point(Cx + half - 20, surfaceY + 0.5),
                new Point(Cx + half - 6, surfaceY + 3));
            sctx.EndFigure(false);
        }
        context.DrawGeometry(null, SandShinePen, shine);
    }

    private void DrawSandStream(DrawingContext context, double p)
    {
        var topStreamY = Math.Min(Math.Max(TopSandSurfaceY(p), WaistY - 24), WaistY - 2);
        var bottomStreamY = Math.Max(BottomSandSurfaceY(p), WaistY + 4);
        if (bottomStreamY - topStreamY < 4) return;
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(new Point(Cx - 1.7, topStreamY), true);
        ctx.CubicBezierTo(
            new Point(Cx - 1.5, WaistY - 2),
            new Point(Cx - 2.7, WaistY + 4),
            new Point(Cx - 2.3, bottomStreamY));
        ctx.LineTo(new Point(Cx + 2.3, bottomStreamY));
        ctx.CubicBezierTo(
            new Point(Cx + 2.7, WaistY + 4),
            new Point(Cx + 1.5, WaistY - 2),
            new Point(Cx + 1.7, topStreamY));
        context.DrawGeometry(SandBrush, null, g);
    }
}