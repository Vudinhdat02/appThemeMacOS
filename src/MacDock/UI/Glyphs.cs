using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WShapes = System.Windows.Shapes;

namespace MacDock;

/// <summary>Icon vector tự vẽ (không dùng tài nguyên của Apple). Hệ toạ độ 16x16.</summary>
public static class Glyphs
{
    static Geometry P(string data) { var g = Geometry.Parse(data); g.Freeze(); return g; }

    // Logo MacDock: bông hoa bốn cánh bo tròn + chấm giữa
    public static readonly Geometry Logo = CreateLogo();
    static Geometry CreateLogo()
    {
        var g = new GeometryGroup { FillRule = FillRule.Nonzero };
        for (int i = 0; i < 4; i++)
        {
            var e = new EllipseGeometry(new Point(8, 4.6), 2.6, 3.6);
            e.Transform = new RotateTransform(i * 90 + 45, 8, 8);
            g.Children.Add(e);
        }
        var combined = Geometry.Combine(g, new EllipseGeometry(new Point(8, 8), 1.7, 1.7), GeometryCombineMode.Exclude, null);
        combined.Freeze();
        return combined;
    }

    public static readonly Geometry Bell = P("M8,1.6 C5.6,1.6 4.1,3.4 4.1,5.8 L4.1,8.9 L2.6,11 L13.4,11 L11.9,8.9 L11.9,5.8 C11.9,3.4 10.4,1.6 8,1.6 Z M6.3,12.2 A1.75,1.75 0 0 0 9.7,12.2 Z");
    public static readonly Geometry Speaker = P("M1.8,5.8 L4.6,5.8 L8.2,2.8 L8.2,13.2 L4.6,10.2 L1.8,10.2 Z");
    public static readonly Geometry Bluetooth = P("M4.6,4.9 L11,10.4 L8,13.1 L8,2.9 L11,5.6 L4.6,11.1");
    public static readonly Geometry Moon = P("M9.6,1.8 A6.2,6.2 0 1 0 14.4,10.6 A5,5 0 0 1 9.6,1.8 Z");
    public static readonly Geometry Bolt = P("M9.4,1 L3.4,9.2 L7.4,9.2 L6.4,15 L12.6,6.6 L8.6,6.6 Z");
    public static readonly Geometry Ethernet = P("M2.5,3 L13.5,3 L13.5,10 L10.5,10 L10.5,12.5 L5.5,12.5 L5.5,10 L2.5,10 Z");
    public static readonly Geometry Info = P("M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M7.2,7 L8.8,7 L8.8,12 L7.2,12 Z M8,3.8 A1,1 0 1 1 7.99,3.8 Z");
    public static readonly Geometry DockShape = P("M1.5,3 L14.5,3 L14.5,8 L1.5,8 Z M2,10.5 L14,10.5 A1.5,1.5 0 0 1 14,13.5 L2,13.5 A1.5,1.5 0 0 1 2,10.5 Z");
    public static readonly Geometry MenuBarShape = P("M1,2 L15,2 L15,5 L1,5 Z M2,7 L14,7 L14,14 L2,14 Z");
    public static readonly Geometry Palette = P("M8,1.5 C4.4,1.5 1.5,4.2 1.5,7.6 C1.5,11.2 4.4,14.5 7.8,14.5 C9,14.5 9.4,13.6 9,12.8 C8.5,11.7 9.1,10.6 10.4,10.6 L12,10.6 C13.5,10.6 14.5,9.5 14.5,7.9 C14.5,4.4 11.6,1.5 8,1.5 Z M4.6,8.6 A1.1,1.1 0 1 1 4.59,8.6 Z M5.6,4.9 A1.1,1.1 0 1 1 5.59,4.9 Z M9,3.6 A1.1,1.1 0 1 1 8.99,3.6 Z M11.6,5.8 A1.1,1.1 0 1 1 11.59,5.8 Z");
    public static readonly Geometry Grid = P("M2,2 h5 v5 h-5 Z M9,2 h5 v5 h-5 Z M2,9 h5 v5 h-5 Z M9,9 h5 v5 h-5 Z");
    public static readonly Geometry Gear = CreateGear();
    public static readonly Geometry Calendar = P("M2,3.5 L14,3.5 L14,14 L2,14 Z M2,6.5 L14,6.5 M5,2 L5,4.5 M11,2 L11,4.5");
    public static readonly Geometry Lock = P("M4,7 L12,7 L12,14 L4,14 Z M5.5,7 L5.5,5 A2.5,2.5 0 0 1 10.5,5 L10.5,7");
    public static readonly Geometry Power = P("M8,1.8 L8,7.5 M4.6,3.8 A5.3,5.3 0 1 0 11.4,3.8");
    public static readonly Geometry Sun = CreateSun();

    static Geometry CreateGear()
    {
        var g = new GeometryGroup { FillRule = FillRule.EvenOdd };
        var teeth = new GeometryGroup { FillRule = FillRule.Nonzero };
        teeth.Children.Add(new EllipseGeometry(new Point(8, 8), 5, 5));
        for (int i = 0; i < 8; i++)
        {
            var r = new RectangleGeometry(new Rect(6.8, 0.8, 2.4, 3.2), 0.6, 0.6) { Transform = new RotateTransform(i * 45, 8, 8) };
            teeth.Children.Add(r);
        }
        var res = Geometry.Combine(teeth, new EllipseGeometry(new Point(8, 8), 2.2, 2.2), GeometryCombineMode.Exclude, null);
        res.Freeze();
        return res;
    }

    static Geometry CreateSun()
    {
        var g = new GeometryGroup();
        g.Children.Add(new EllipseGeometry(new Point(8, 8), 3, 3));
        for (int i = 0; i < 8; i++)
            g.Children.Add(new RectangleGeometry(new Rect(7.3, 0.6, 1.4, 2.6), 0.7, 0.7) { Transform = new RotateTransform(i * 45, 8, 8) });
        g.Freeze();
        return g;
    }

    public static readonly Geometry WifiFull = CreateWifiFull();
    static Geometry CreateWifiFull()
    {
        var g = new GeometryGroup();
        foreach (var r in new[] { 3.6, 7.0, 10.4 }) g.Children.Add(WifiArc(r));
        g.Children.Add(new EllipseGeometry(new Point(8, 13.2), 0.6, 0.6));
        g.Freeze();
        return g;
    }

    public static Geometry WifiArc(double r)
    {
        // cung tròn quanh tâm (8, 13), từ -135° đến -45°
        Point c = new(8, 13.2);
        double a1 = -135 * Math.PI / 180, a2 = -45 * Math.PI / 180;
        var fig = new PathFigure { StartPoint = new Point(c.X + r * Math.Cos(a1), c.Y + r * Math.Sin(a1)), IsClosed = false };
        fig.Segments.Add(new ArcSegment(new Point(c.X + r * Math.Cos(a2), c.Y + r * Math.Sin(a2)), new Size(r, r), 0, false, SweepDirection.Clockwise, true));
        var pg = new PathGeometry(new[] { fig });
        pg.Freeze();
        return pg;
    }

    /// <summary>Tạo phần tử Path vẽ glyph, kích thước <paramref name="size"/>.</summary>
    public static FrameworkElement Icon(Geometry g, Brush fill, double size = 15, bool stroke = false, double thickness = 1.4)
    {
        var path = new WShapes.Path
        {
            Data = g,
            Stretch = Stretch.None,
            SnapsToDevicePixels = false,
        };
        if (stroke)
        {
            path.Stroke = fill; path.StrokeThickness = thickness;
            path.StrokeStartLineCap = PenLineCap.Round; path.StrokeEndLineCap = PenLineCap.Round; path.StrokeLineJoin = PenLineJoin.Round;
        }
        else path.Fill = fill;
        return new Viewbox { Width = size, Height = size, Child = new Canvas { Width = 16, Height = 16, Children = { path } }, Stretch = Stretch.Uniform };
    }

    public static FrameworkElement WifiIcon(Brush fg, int signal, double size = 16)
    {
        var canvas = new Canvas { Width = 16, Height = 16 };
        int bars = signal >= 75 ? 3 : signal >= 45 ? 2 : signal > 0 ? 1 : 0;
        double[] radii = { 3.6, 7, 10.4 };
        for (int i = 0; i < 3; i++)
        {
            var p = new WShapes.Path
            {
                Data = WifiArc(radii[i]), Stroke = fg, StrokeThickness = 1.9,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                Opacity = i < bars ? 1 : 0.28
            };
            canvas.Children.Add(p);
        }
        var dot = new WShapes.Ellipse { Width = 2.6, Height = 2.6, Fill = fg };
        Canvas.SetLeft(dot, 6.7); Canvas.SetTop(dot, 11.9);
        canvas.Children.Add(dot);
        return new Viewbox { Width = size, Height = size, Child = canvas };
    }

    public static FrameworkElement VolumeIcon(Brush fg, double level, bool muted, double size = 16)
    {
        var canvas = new Canvas { Width = 16, Height = 16 };
        canvas.Children.Add(new WShapes.Path { Data = Speaker, Fill = fg });
        if (muted || level <= 0.001)
        {
            canvas.Children.Add(new WShapes.Path { Data = P("M10.6,5.8 L14.4,9.8 M14.4,5.8 L10.6,9.8"), Stroke = fg, StrokeThickness = 1.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        }
        else
        {
            int waves = level > 0.66 ? 3 : level > 0.33 ? 2 : 1;
            double[] r = { 2.4, 4.6, 6.8 };
            for (int i = 0; i < 3; i++)
            {
                var c = new Point(8.4, 8);
                double a = 50 * Math.PI / 180;
                var fig = new PathFigure { StartPoint = new Point(c.X + r[i] * Math.Cos(-a), c.Y + r[i] * Math.Sin(-a)) };
                fig.Segments.Add(new ArcSegment(new Point(c.X + r[i] * Math.Cos(a), c.Y + r[i] * Math.Sin(a)), new Size(r[i], r[i]), 0, false, SweepDirection.Clockwise, true));
                canvas.Children.Add(new WShapes.Path { Data = new PathGeometry(new[] { fig }), Stroke = fg, StrokeThickness = 1.3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Opacity = i < waves ? 1 : 0.25 });
            }
        }
        return new Viewbox { Width = size, Height = size, Child = canvas };
    }

    public static FrameworkElement BatteryIcon(Brush fg, int percent, bool charging, double height = 12)
    {
        var canvas = new Canvas { Width = 26, Height = 12 };
        var body = new WShapes.Rectangle { Width = 22, Height = 11, RadiusX = 3, RadiusY = 3, Stroke = fg, StrokeThickness = 1, Opacity = 0.55 };
        Canvas.SetLeft(body, 0.5); Canvas.SetTop(body, 0.5);
        canvas.Children.Add(body);
        var nub = new WShapes.Rectangle { Width = 1.8, Height = 4, RadiusX = 1, RadiusY = 1, Fill = fg, Opacity = 0.55 };
        Canvas.SetLeft(nub, 23.4); Canvas.SetTop(nub, 4);
        canvas.Children.Add(nub);
        Brush fillBrush = percent <= 15 && !charging ? new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30)) : charging ? new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59)) : fg;
        var fill = new WShapes.Rectangle { Width = Math.Max(1.5, 19 * Math.Clamp(percent, 0, 100) / 100.0), Height = 8, RadiusX = 1.6, RadiusY = 1.6, Fill = fillBrush };
        Canvas.SetLeft(fill, 2); Canvas.SetTop(fill, 2);
        canvas.Children.Add(fill);
        if (charging)
        {
            var bolt = new WShapes.Path { Data = Bolt, Fill = fg, Stroke = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)), StrokeThickness = 0.6, RenderTransform = new ScaleTransform(0.7, 0.7) };
            Canvas.SetLeft(bolt, 6.5); Canvas.SetTop(bolt, 0.3);
            canvas.Children.Add(bolt);
        }
        return new Viewbox { Height = height, Width = height * 26 / 12, Child = canvas };
    }

    public static FrameworkElement ControlCenterIcon(Brush fg, double size = 16)
    {
        var canvas = new Canvas { Width = 16, Height = 16 };
        var r1 = new WShapes.Rectangle { Width = 13, Height = 5.6, RadiusX = 2.8, RadiusY = 2.8, Stroke = fg, StrokeThickness = 1.2 };
        Canvas.SetLeft(r1, 1.5); Canvas.SetTop(r1, 1.8); canvas.Children.Add(r1);
        var c1 = new WShapes.Ellipse { Width = 3.4, Height = 3.4, Fill = fg };
        Canvas.SetLeft(c1, 9.9); Canvas.SetTop(c1, 2.9); canvas.Children.Add(c1);
        var r2 = new WShapes.Rectangle { Width = 13, Height = 5.6, RadiusX = 2.8, RadiusY = 2.8, Stroke = fg, StrokeThickness = 1.2 };
        Canvas.SetLeft(r2, 1.5); Canvas.SetTop(r2, 8.8); canvas.Children.Add(r2);
        var c2 = new WShapes.Ellipse { Width = 3.4, Height = 3.4, Fill = fg };
        Canvas.SetLeft(c2, 2.7); Canvas.SetTop(c2, 9.9); canvas.Children.Add(c2);
        return new Viewbox { Width = size, Height = size, Child = canvas };
    }

    // ---------------- Icon lớn cho Dock (vẽ bằng DrawingContext) ----------------

    public static Geometry Squircle(Rect r)
    {
        double rad = Math.Min(r.Width, r.Height) * 0.225;
        var g = new RectangleGeometry(r, rad, rad);
        g.Freeze();
        return g;
    }

    static readonly Color[] _lpColors =
    {
        Color.FromRgb(0xFF,0x5F,0x57), Color.FromRgb(0xFF,0xBD,0x2E), Color.FromRgb(0x28,0xC8,0x40),
        Color.FromRgb(0x0A,0x84,0xFF), Color.FromRgb(0xBF,0x5A,0xF2), Color.FromRgb(0xFF,0x37,0x5F),
        Color.FromRgb(0x64,0xD2,0xFF), Color.FromRgb(0xFF,0x9F,0x0A), Color.FromRgb(0x30,0xD1,0x58),
    };

    static DrawingImage? _launchpad, _trashEmpty, _trashFull, _appIcon;

    public static DrawingImage Launchpad => _launchpad ??= BuildLaunchpad();
    static DrawingImage BuildLaunchpad()
    {
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            var r = new Rect(4, 4, 120, 120);
            var bg = new LinearGradientBrush(Color.FromRgb(0x5B, 0x5F, 0x6E), Color.FromRgb(0x2E, 0x31, 0x3B), 90);
            dc.DrawGeometry(bg, new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)), 1.5), Squircle(r));
            double cell = 26, gap = 9, start = 64 - (3 * cell + 2 * gap) / 2;
            for (int i = 0; i < 9; i++)
            {
                int x = i % 3, y = i / 3;
                var cr = new Rect(start + x * (cell + gap), start + y * (cell + gap), cell, cell);
                var c = _lpColors[i];
                var br = new LinearGradientBrush(Color.FromRgb((byte)Math.Min(255, c.R + 40), (byte)Math.Min(255, c.G + 40), (byte)Math.Min(255, c.B + 40)), c, 90);
                dc.DrawRoundedRectangle(br, null, cr, 7, 7);
            }
        }
        dg.Freeze();
        var img = new DrawingImage(dg); img.Freeze();
        return img;
    }

    static DrawingImage? _winLogo;
    /// <summary>Biểu tượng bốn ô vuông xanh kiểu Windows 11 (vẽ vector).</summary>
    public static DrawingImage WindowsLogo => _winLogo ??= BuildWindowsLogo();
    static DrawingImage BuildWindowsLogo()
    {
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 128, 128));
            var br = new LinearGradientBrush(Color.FromRgb(0x2B, 0xC4, 0xF3), Color.FromRgb(0x00, 0x67, 0xC0), new Point(0, 0), new Point(1, 1));
            br.Freeze();
            double s = 52, gap = 6, o = 64 - s - gap / 2;
            for (int i = 0; i < 4; i++)
                dc.DrawRoundedRectangle(br, null, new Rect(o + (i % 2) * (s + gap), o + (i / 2) * (s + gap), s, s), 3, 3);
        }
        dg.Freeze();
        var img = new DrawingImage(dg); img.Freeze();
        return img;
    }

    public static DrawingImage Trash(bool full) => full ? (_trashFull ??= BuildTrash(true)) : (_trashEmpty ??= BuildTrash(false));
    static DrawingImage BuildTrash(bool full)
    {
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 128, 128));
            // thân thùng (trụ, hơi thu nhỏ dưới đáy)
            var body = Geometry.Parse("M24,30 L104,30 L96,118 Q95,124 89,124 L39,124 Q33,124 32,118 Z");
            var glass = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(0xE6, 0xF4, 0xF6, 0xF9), 0),
                new GradientStop(Color.FromArgb(0xD9, 0xC9, 0xCF, 0xD8), 0.5),
                new GradientStop(Color.FromArgb(0xE6, 0xEE, 0xF1, 0xF5), 1),
            }, new Point(0, 0.5), new Point(1, 0.5));
            if (full)
            {
                var paper = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xF7));
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC0)), 1.5);
                dc.DrawGeometry(paper, pen, Geometry.Parse("M34,34 L50,12 L66,26 L80,8 L96,30 Z"));
                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0xFF, 0xE8, 0xA8)), pen, Geometry.Parse("M52,32 L60,16 L78,22 L74,34 Z"));
            }
            dc.DrawGeometry(glass, new Pen(new SolidColorBrush(Color.FromArgb(0xB0, 0x8A, 0x92, 0x9E)), 2), body);
            var linePen = new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0x7A, 0x82, 0x8E)), 2.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            for (int i = 0; i < 5; i++)
            {
                double xt = 38 + i * 13, xb = 43 + i * 10.5;
                dc.DrawLine(linePen, new Point(xt, 42), new Point(xb, 114));
            }
            // vành miệng
            dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(0xF7, 0xF8, 0xFA), Color.FromRgb(0xB9, 0xC0, 0xCA), 90),
                new Pen(new SolidColorBrush(Color.FromArgb(0xB0, 0x8A, 0x92, 0x9E)), 1.5), new Rect(20, 26, 88, 9), 4.5, 4.5);
        }
        dg.Freeze();
        var img = new DrawingImage(dg); img.Freeze();
        return img;
    }

    /// <summary>Icon ứng dụng MacDock (dùng cho cửa sổ Cài đặt).</summary>
    public static DrawingImage AppIcon => _appIcon ??= BuildAppIcon();
    static DrawingImage BuildAppIcon()
    {
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            var bg = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Color.FromRgb(0x4F, 0xA8, 0xFF), 0),
                new GradientStop(Color.FromRgb(0x6A, 0x5C, 0xFF), 1)
            }, 90);
            dc.DrawGeometry(bg, null, Squircle(new Rect(4, 4, 120, 120)));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)), null, new Rect(20, 84, 88, 22), 11, 11);
            var dot = new SolidColorBrush(Color.FromRgb(0x5B, 0x7C, 0xFF));
            for (int i = 0; i < 4; i++) dc.DrawRoundedRectangle(dot, null, new Rect(28 + i * 19, 88, 14, 14), 4, 4);
            var logo = Logo.Clone();
            logo.Transform = new MatrixTransform(4.2, 0, 0, 4.2, 64 - 8 * 4.2, 44 - 8 * 4.2);
            dc.DrawGeometry(Brushes.White, null, logo);
        }
        dg.Freeze();
        var img = new DrawingImage(dg); img.Freeze();
        return img;
    }
}
