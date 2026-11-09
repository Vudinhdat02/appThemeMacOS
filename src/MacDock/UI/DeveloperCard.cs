using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WShapes = System.Windows.Shapes;

namespace MacDock;

/// <summary>Thông tin nhà phát triển cho trang Giới thiệu.</summary>
public static class Developer
{
    public const string Name = "Vũ Đình Đạt";
    public const string Bank = "Ngân hàng MB Bank";
    public const string GitHub = "https://github.com/Vudinhdat02";
    public const string Email = "vudinhdat02@gmail.com";
    public const string Repo = "https://github.com/Vudinhdat02/appThemeMacOS";
    // Điền link khi có; để trống thì nút sẽ báo "chưa có liên kết"
    public const string Facebook = "";
    public const string YouTube = "";
    public const string TikTok = "";

    static BitmapImage Res(string name)
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.UriSource = new Uri($"pack://application:,,,/MacDock;component/Assets/{name}");
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.DecodePixelWidth = 400;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    /// <summary>Hàng 1: ảnh tròn + mã QR (cùng kích thước) kèm chú thích; hàng 2: các nút liên hệ.</summary>
    public static FrameworkElement Build()
    {
        const double size = 132;
        var t = ThemeManager.Current;
        var root = new StackPanel { Margin = new Thickness(0, 18, 0, 18), HorizontalAlignment = HorizontalAlignment.Center };

        var row1 = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });

        var photo = new WShapes.Ellipse
        {
            Width = size, Height = size,
            Fill = new ImageBrush(Res("dat.jpeg")) { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Top },
            Stroke = t.Accent, StrokeThickness = 3,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.25, Direction = 270 }
        };
        var qr = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(16), Background = Brushes.White, Padding = new Thickness(8),
            BorderBrush = t.Accent, BorderThickness = new Thickness(3),
            Child = new Image { Source = Res("qr.jpeg"), Stretch = Stretch.Uniform },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.25, Direction = 270 }
        };
        RenderOptions.SetBitmapScalingMode(qr, BitmapScalingMode.NearestNeighbor);
        row1.Children.Add(Column(photo, "Developer", Name, 0));
        row1.Children.Add(Column(qr, "Donate", Bank, 1));
        root.Children.Add(row1);

        var row2 = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 22, 0, 0) };
        row2.Children.Add(Contact(GitHubIcon(), "GitHub", GitHub));
        row2.Children.Add(Contact(GmailIcon(), "Gmail", "mailto:" + Email));
        row2.Children.Add(Contact(FacebookIcon(), "Facebook", Facebook));
        row2.Children.Add(Contact(YouTubeIcon(), "YouTube", YouTube));
        row2.Children.Add(Contact(TikTokIcon(), "TikTok", TikTok));
        root.Children.Add(row2);
        return root;
    }

    static FrameworkElement Column(FrameworkElement img, string caption, string value, int col)
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        img.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(img);
        var cap = new TextBlock { Text = caption, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0), FontSize = 12, Opacity = 0.6 };
        sp.Children.Add(cap);
        sp.Children.Add(new TextBlock { Text = value, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 15, FontWeight = FontWeights.SemiBold });
        Grid.SetColumn(sp, col);
        return sp;
    }

    static FrameworkElement Contact(FrameworkElement icon, string label, string url)
    {
        var sp = new StackPanel { Margin = new Thickness(10, 0, 10, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent, ToolTip = string.IsNullOrEmpty(url) ? label : url.Replace("mailto:", "") };
        icon.Width = 40; icon.Height = 40;
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(icon);
        sp.Children.Add(new TextBlock { Text = label, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0), Opacity = 0.75 });
        sp.MouseEnter += (_, _) => sp.Opacity = 0.75;
        sp.MouseLeave += (_, _) => sp.Opacity = 1;
        sp.MouseLeftButtonUp += (_, _) =>
        {
            if (string.IsNullOrEmpty(url)) MessageBox.Show($"Chưa có liên kết {label}.", "MacDock");
            else Launcher.Open(url);
        };
        return sp;
    }

    static FrameworkElement Tile(Brush bg, UIElement content, double radius = 10)
        => new Border { Background = bg, CornerRadius = new CornerRadius(radius), Child = content, ClipToBounds = true };

    static FrameworkElement Vector(string data, Brush fill, double scale, double ox, double oy)
    {
        var g = Geometry.Parse(data);
        return new Viewbox { Child = new Canvas { Width = 40, Height = 40, Children = { new WShapes.Path { Data = g, Fill = fill, RenderTransform = new MatrixTransform(scale, 0, 0, scale, ox, oy) } } } };
    }

    static FrameworkElement GitHubIcon()
    {
        // Octicons mark-github (MIT)
        const string d = "M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z";
        return Tile(new SolidColorBrush(Color.FromRgb(0x18, 0x17, 0x17)), Vector(d, Brushes.White, 1.6, 7.2, 7.2));
    }

    static FrameworkElement GmailIcon()
    {
        var c = new Canvas { Width = 40, Height = 40 };
        c.Children.Add(new WShapes.Rectangle { Width = 40, Height = 40, RadiusX = 10, RadiusY = 10, Fill = Brushes.White, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), StrokeThickness = 1 });
        c.Children.Add(new WShapes.Path { Data = Geometry.Parse("M8,13 L8,29 L13,29 L13,18 L20,23.5 L27,18 L27,29 L32,29 L32,13 L29,11 L20,18 L11,11 Z"), Fill = new SolidColorBrush(Color.FromRgb(0xEA, 0x43, 0x35)) });
        c.Children.Add(new WShapes.Path { Data = Geometry.Parse("M8,13 L13,17 L13,29 L9.5,29 Q8,29 8,27.5 Z"), Fill = new SolidColorBrush(Color.FromRgb(0x42, 0x85, 0xF4)) });
        c.Children.Add(new WShapes.Path { Data = Geometry.Parse("M32,13 L27,17 L27,29 L30.5,29 Q32,29 32,27.5 Z"), Fill = new SolidColorBrush(Color.FromRgb(0x34, 0xA8, 0x53)) });
        return new Viewbox { Child = c };
    }

    static FrameworkElement Letter(Brush bg, string letter, Brush fg, double radius, double size, double dy = 0)
    {
        var tb = new TextBlock { Text = letter, FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.Bold, FontSize = size, Foreground = fg, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4, 0, 0, dy) };
        return new Viewbox { Child = new Border { Width = 40, Height = 40, Background = bg, CornerRadius = new CornerRadius(radius), Child = tb, ClipToBounds = true } };
    }

    static FrameworkElement FacebookIcon() => Letter(new SolidColorBrush(Color.FromRgb(0x08, 0x66, 0xFF)), "f", Brushes.White, 20, 34, -6);

    static FrameworkElement YouTubeIcon()
    {
        var c = new Canvas { Width = 40, Height = 40 };
        c.Children.Add(new WShapes.Rectangle { Width = 40, Height = 40, RadiusX = 10, RadiusY = 10, Fill = Brushes.White, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), StrokeThickness = 1 });
        var red = new WShapes.Rectangle { Width = 30, Height = 21, RadiusX = 6, RadiusY = 6, Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)) };
        Canvas.SetLeft(red, 5); Canvas.SetTop(red, 9.5); c.Children.Add(red);
        c.Children.Add(new WShapes.Path { Data = Geometry.Parse("M17,15 L25,20 L17,25 Z"), Fill = Brushes.White });
        return new Viewbox { Child = c };
    }

    static FrameworkElement TikTokIcon()
    {
        const string note = "M22,8 L26,8 Q26.5,12.5 31,13 L31,17 Q27.5,17 26,15.5 L26,24 A7,7 0 1 1 19,17 L20,17 L20,21 A3,3 0 1 0 22,24 Z";
        var c = new Canvas { Width = 40, Height = 40 };
        c.Children.Add(new WShapes.Rectangle { Width = 40, Height = 40, RadiusX = 10, RadiusY = 10, Fill = Brushes.Black });
        c.Children.Add(new WShapes.Path { Data = Geometry.Parse(note), Fill = new SolidColorBrush(Color.FromRgb(0x25, 0xF4, 0xEE)), RenderTransform = new TranslateTransform(-1.2, -1) });
        c.Children.Add(new WShapes.Path { Data = Geometry.Parse(note), Fill = new SolidColorBrush(Color.FromRgb(0xFE, 0x2C, 0x55)), RenderTransform = new TranslateTransform(1.2, 1) });
        c.Children.Add(new WShapes.Path { Data = Geometry.Parse(note), Fill = Brushes.White });
        return new Viewbox { Child = c };
    }
}
