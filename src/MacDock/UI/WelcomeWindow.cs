using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WShapes = System.Windows.Shapes;

namespace MacDock;

/// <summary>Màn hình chào lần đầu, phong cách macOS 26.</summary>
public sealed class WelcomeWindow : GlassWindow
{
    readonly Grid _pageHost = new() { Height = 470 };
    readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 18) };
    readonly List<FrameworkElement> _pages = new();
    int _page;
    readonly bool _reviewOnly;
    CheckBox _auto = null!, _hide = null!, _bar = null!, _previews = null!;

    public WelcomeWindow(bool reviewOnly = false) : base(760, closeButton: true, tintOpacity: 0.62)
    {
        _reviewOnly = reviewOnly;
        Title = "Chào mừng đến với MacDock";
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = !reviewOnly;

        var root = new Grid { ClipToBounds = true };
        root.Children.Add(Blobs());
        var layout = new DockPanel();
        DockPanel.SetDock(_dots, Dock.Bottom);
        layout.Children.Add(_dots);
        layout.Children.Add(_pageHost);
        root.Children.Add(layout);
        Body = root;

        _pages.Add(PageHello());
        _pages.Add(PageFeatures());
        _pages.Add(PageSetup());
        _pages.Add(PageDone());
        foreach (var p in _pages) { p.Visibility = Visibility.Collapsed; _pageHost.Children.Add(p); }
        for (int i = 0; i < _pages.Count; i++)
            _dots.Children.Add(new WShapes.Ellipse { Width = 7, Height = 7, Margin = new Thickness(4, 0, 4, 0), Fill = ThemeManager.Current.PopupForeground });
        Go(0);

        Closed += (_, _) =>
        {
            var g = Config.Current.General;
            g.AutoStart = _auto.IsChecked == true;
            g.HideTaskbar = _hide.IsChecked == true;
            Config.Current.MenuBar.Enabled = _bar.IsChecked == true;
            Config.Current.Dock.WindowPreviews = _previews.IsChecked == true;
            if (_reviewOnly) { Config.Touch("general"); Config.Touch("menubar"); Config.Touch("dock"); Config.Touch("theme"); return; }
            g.FirstRunDone = true;
            Config.Save();
        };
    }

    public void TestGo(int i) => Go(i);

    void Go(int i)
    {
        _page = Math.Clamp(i, 0, _pages.Count - 1);
        for (int k = 0; k < _pages.Count; k++)
        {
            var p = _pages[k];
            if (k == _page)
            {
                p.Visibility = Visibility.Visible;
                if (!App.EffectsOff)
                {
                    p.RenderTransform = new TranslateTransform(30, 0);
                    p.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)));
                    ((TranslateTransform)p.RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(380)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                }
            }
            else p.Visibility = Visibility.Collapsed;
            if (_dots.Children.Count > k) ((WShapes.Ellipse)_dots.Children[k]).Opacity = k == _page ? 0.85 : 0.25;
        }
    }

    // ------------------------------------------------------------------ trang

    FrameworkElement PageHello()
    {
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var grad = new LinearGradientBrush(new GradientStopCollection
        {
            new(Color.FromRgb(0x3A, 0x8D, 0xFF), 0), new(Color.FromRgb(0xA8, 0x5C, 0xF7), 0.35), new(Color.FromRgb(0xFF, 0x6B, 0x8B), 0.7), new(Color.FromRgb(0xFF, 0xB2, 0x4D), 1)
        }, new Point(0, 0.5), new Point(1, 0.5)) { MappingMode = BrushMappingMode.RelativeToBoundingBox };
        var hello = new TextBlock { Text = "xin chào", FontSize = 96, FontWeight = FontWeights.Light, FontStyle = FontStyles.Italic, Foreground = grad, HorizontalAlignment = HorizontalAlignment.Center, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI Light, Segoe UI") };
        if (!App.EffectsOff)
        {
            var tt = new TranslateTransform();
            grad.RelativeTransform = tt;
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-0.6, 0.6, TimeSpan.FromSeconds(4)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            hello.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1.2)));
        }
        sp.Children.Add(hello);
        sp.Children.Add(new TextBlock { Text = "Chào mừng đến với MacDock", FontSize = 20, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 6) });
        sp.Children.Add(new TextBlock { Text = "Dock, Thanh Menu và giao diện kiểu macOS cho Windows 11", Opacity = 0.65, HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(ArrowButton(() => Go(1), 30));
        return sp;
    }

    FrameworkElement PageFeatures()
    {
        var sp = Page("Có gì trong MacDock");
        foreach (var (glyph, color, title, desc) in new (Geometry, Color, string, string)[]
        {
            (Glyphs.DockShape, Color.FromRgb(0x0A, 0x84, 0xFF), "Dock", "Phóng to khi rê chuột, xem trước cửa sổ, kéo thả để sắp xếp và ghim ứng dụng."),
            (Glyphs.MenuBarShape, Color.FromRgb(0x5E, 0x5C, 0xE6), "Thanh Menu", "Menu của ứng dụng đang dùng, Wi‑Fi, âm lượng, pin, đồng hồ và icon khay hệ thống."),
            (Glyphs.Palette, Color.FromRgb(0xFF, 0x9F, 0x0A), "Giao diện", "6 chủ đề dựng sẵn, sáng/tối theo Windows hoặc theo giờ, nhập/xuất theme."),
            (Glyphs.Bolt, Color.FromRgb(0x30, 0xD1, 0x58), "Nhẹ và an toàn", "Gần như không tốn CPU khi rảnh; taskbar gốc luôn được khôi phục khi thoát."),
        })
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 16), MaxWidth = 520 };
            var tile = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(color), Child = Glyphs.Icon(glyph, Brushes.White, 22), Margin = new Thickness(0, 0, 16, 0) };
            DockPanel.SetDock(tile, Dock.Left);
            row.Children.Add(tile);
            var tx = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            tx.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
            tx.Children.Add(new TextBlock { Text = desc, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(tx);
            sp.Children.Add(row);
        }
        sp.Children.Add(NavButtons(0, 2));
        return sp;
    }

    FrameworkElement PageSetup()
    {
        var sp = Page("Thiết lập nhanh");
        var g = Config.Current.General;
        var card = new Border { CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)), Padding = new Thickness(18, 6, 18, 6), MaxWidth = 520, BorderThickness = new Thickness(0.6), BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x88)) };
        var list = new StackPanel();
        CheckBox Opt(string text, string desc, bool value)
        {
            var cb = new CheckBox { IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
            cb.SetResourceReference(StyleProperty, "Md.Toggle");
            var row = new DockPanel { Margin = new Thickness(0, 9, 0, 9) };
            DockPanel.SetDock(cb, Dock.Right);
            row.Children.Add(cb);
            var tx = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            tx.Children.Add(new TextBlock { Text = text });
            tx.Children.Add(new TextBlock { Text = desc, FontSize = 11.5, Opacity = 0.6, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(tx);
            list.Children.Add(row);
            return cb;
        }
        _auto = Opt("Khởi động cùng Windows", "Tự chạy MacDock khi đăng nhập", g.AutoStart);
        _hide = Opt("Ẩn thanh tác vụ của Windows", "Icon khay hệ thống sẽ hiện trên Thanh Menu. Bật lại bất cứ lúc nào.", g.HideTaskbar);
        _bar = Opt("Hiện Thanh Menu", "Thanh menu ở cạnh trên màn hình", Config.Current.MenuBar.Enabled);
        _previews = Opt("Xem trước cửa sổ trên Dock", "Rê chuột lên ứng dụng đang chạy để xem các cửa sổ", Config.Current.Dock.WindowPreviews);
        card.Child = list;
        sp.Children.Add(card);

        var themes = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) };
        foreach (var t in ThemeManager.BuiltIns)
        {
            var id = t.Id;
            var p = ThemeManager.WantDark() ? t.Dark : t.Light;
            Color C(string s) { try { return (Color)ColorConverter.ConvertFromString(s); } catch { return Colors.Gray; } }
            var sw = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Margin = new Thickness(6, 0, 6, 0), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = t.Name,
                Background = new LinearGradientBrush(C(p.MenuBarBackground), C(p.Accent), 45), BorderThickness = new Thickness(Config.Current.Theme.ThemeId == id ? 3 : 1), BorderBrush = ThemeManager.Current.Accent };
            sw.MouseLeftButtonUp += (_, _) =>
            {
                Config.Current.Theme.ThemeId = id;
                ThemeManager.Apply();
                foreach (Border b in themes.Children) b.BorderThickness = new Thickness(1);
                sw.BorderThickness = new Thickness(3);
            };
            themes.Children.Add(sw);
        }
        sp.Children.Add(new TextBlock { Text = "Chọn chủ đề", HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.6, Margin = new Thickness(0, 14, 0, 0), FontSize = 12 });
        sp.Children.Add(themes);
        sp.Children.Add(NavButtons(1, 3));
        return sp;
    }

    FrameworkElement PageDone()
    {
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new Image { Source = Glyphs.AppIcon, Width = 96, Height = 96 });
        sp.Children.Add(new TextBlock { Text = "Đã sẵn sàng", FontSize = 30, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 6) });
        sp.Children.Add(new TextBlock { Text = "Chuột phải lên Dock để đổi giao diện nhanh, hoặc mở Cài đặt từ biểu tượng góc trên bên trái.", Opacity = 0.65, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 440 });
        sp.Children.Add(new TextBlock { Text = "Nếu taskbar không hiện lại vì lý do nào đó: MacDock.exe --restore-taskbar", Opacity = 0.45, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
        var start = PillButton(_reviewOnly ? "Xong" : "Bắt đầu sử dụng", true);
        start.Padding = new Thickness(36, 9, 36, 9);
        start.FontSize = 14;
        start.Margin = new Thickness(0, 26, 0, 0);
        start.HorizontalAlignment = HorizontalAlignment.Center;
        start.IsDefault = true;
        start.Click += (_, _) => Close();
        sp.Children.Add(start);
        var back = new TextBlock { Text = "Quay lại", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0), Cursor = System.Windows.Input.Cursors.Hand, Opacity = 0.6 };
        back.MouseLeftButtonUp += (_, _) => Go(2);
        sp.Children.Add(back);
        return sp;
    }

    // ------------------------------------------------------------------ phần tử

    static StackPanel Page(string title)
    {
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(40, 30, 40, 0) };
        sp.Children.Add(new TextBlock { Text = title, FontSize = 26, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 22) });
        return sp;
    }

    FrameworkElement NavButtons(int back, int next)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) };
        var b = PillButton("Quay lại", false); b.Click += (_, _) => Go(back); b.Margin = new Thickness(0, 0, 10, 0);
        var n = PillButton("Tiếp tục", true); n.Click += (_, _) => Go(next); n.IsDefault = true;
        sp.Children.Add(b); sp.Children.Add(n);
        return sp;
    }

    static FrameworkElement ArrowButton(Action a, double top)
    {
        var b = new Border
        {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(23), Background = ThemeManager.Current.Accent, Margin = new Thickness(0, top, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand,
            Child = new WShapes.Path { Data = Geometry.Parse("M0,6 L14,6 M8,0 L14,6 L8,12"), Stroke = Brushes.White, StrokeThickness = 2.2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.3, Direction = 270 }
        };
        b.MouseEnter += (_, _) => b.Opacity = 0.88;
        b.MouseLeave += (_, _) => b.Opacity = 1;
        b.MouseLeftButtonUp += (_, _) => a();
        return b;
    }

    /// <summary>Các mảng màu mờ trôi chậm phía sau (giống hình nền màn hình chào của macOS).</summary>
    static FrameworkElement Blobs()
    {
        var c = new Canvas { IsHitTestVisible = false, Opacity = 0.55 };
        var colors = new[] { Color.FromRgb(0x3A, 0x8D, 0xFF), Color.FromRgb(0xB0, 0x5C, 0xF7), Color.FromRgb(0xFF, 0x7A, 0x8B), Color.FromRgb(0xFF, 0xC2, 0x4D) };
        var pos = new[] { (-80.0, -60.0), (480.0, -90.0), (520.0, 300.0), (-60.0, 320.0) };
        for (int i = 0; i < 4; i++)
        {
            var e = new WShapes.Ellipse { Width = 360, Height = 300, Fill = new RadialGradientBrush(Color.FromArgb(0xC0, colors[i].R, colors[i].G, colors[i].B), Color.FromArgb(0, colors[i].R, colors[i].G, colors[i].B)) };
            Canvas.SetLeft(e, pos[i].Item1); Canvas.SetTop(e, pos[i].Item2);
            if (!App.EffectsOff)
            {
                var tt = new TranslateTransform();
                e.RenderTransform = tt;
                tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-40, 40, TimeSpan.FromSeconds(7 + i * 1.7)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-30, 30, TimeSpan.FromSeconds(9 + i)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            }
            c.Children.Add(e);
        }
        return c;
    }
}
