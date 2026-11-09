using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace MacDock;

/// <summary>
/// Cửa sổ kiểu macOS 26: không viền, bo góc, nền kính (Acrylic/Mica của Windows 11) phủ một lớp màu theme,
/// nút đóng tròn nhỏ ở góc trái, kéo để di chuyển.
/// </summary>
public class GlassWindow : Window
{
    readonly Grid _root = new();
    readonly ContentControl _body = new();
    readonly Border _tint = new();

    public UIElement Body { set => _body.Content = value; }

    public GlassWindow(double width, bool closeButton = true, double tintOpacity = 0.78)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        Width = width;
        Background = Brushes.Transparent;
        ShowInTaskbar = true;
        Icon = Glyphs.AppIcon;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            GlassFrameThickness = new Thickness(-1),
            ResizeBorderThickness = new Thickness(0),
            UseAeroCaptionButtons = false,
            CornerRadius = new CornerRadius(0)
        });
        SetResourceReference(ForegroundProperty, "Md.PopupFg");
        SetResourceReference(FontFamilyProperty, "Md.Font");
        FontSize = 13;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        var t = ThemeManager.Current;
        var c = ((SolidColorBrush)t.WindowBackground).Color;
        _tint.Background = new SolidColorBrush(Color.FromArgb((byte)(255 * tintOpacity), c.R, c.G, c.B));
        _root.Children.Add(_tint);
        _root.Children.Add(_body);
        if (closeButton)
        {
            var dot = new Border
            {
                Width = 13, Height = 13, CornerRadius = new CornerRadius(6.5), Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x5F, 0x57)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)), BorderThickness = new Thickness(0.5),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(14, 14, 0, 0),
                Cursor = Cursors.Hand, ToolTip = "Đóng"
            };
            var x = new System.Windows.Shapes.Path { Data = Geometry.Parse("M3.5,3.5 L9.5,9.5 M9.5,3.5 L3.5,9.5"), Stroke = new SolidColorBrush(Color.FromArgb(0xA0, 0x4D, 0, 0)), StrokeThickness = 1.3, Visibility = Visibility.Hidden };
            dot.Child = x;
            dot.MouseEnter += (_, _) => x.Visibility = Visibility.Visible;
            dot.MouseLeave += (_, _) => x.Visibility = Visibility.Hidden;
            dot.MouseLeftButtonUp += (_, e) => { e.Handled = true; Close(); };
            dot.MouseLeftButtonDown += (_, e) => e.Handled = true;
            _root.Children.Add(dot);
        }
        Content = _root;
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is FrameworkElement fe && (fe is TextBox || fe.TemplatedParent is TextBox || fe is PasswordBox)) return;
            try { DragMove(); } catch { }
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var h = new WindowInteropHelper(this).Handle;
        var src = HwndSource.FromHwnd(h);
        if (src?.CompositionTarget != null) src.CompositionTarget.BackgroundColor = Colors.Transparent;
        int dark = ThemeManager.Current.IsDark ? 1 : 0;
        Native.DwmSetWindowAttribute(h, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
        int round = 2; // DWMWCP_ROUND
        Native.DwmSetWindowAttribute(h, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
        int backdrop = 3; // DWMSBT_TRANSIENTWINDOW (Acrylic)
        if (Native.DwmSetWindowAttribute(h, Native.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, 4) != 0)
        {
            // Windows cũ: dùng lớp màu đặc
            _tint.Background = ThemeManager.Current.WindowBackground;
        }
        Native.AddExStyle(h, Native.WS_EX_APPWINDOW);
    }

    /// <summary>Nút bo tròn kiểu macOS.</summary>
    public static Button PillButton(string text, bool primary)
    {
        var b = new Button { Content = text, Padding = new Thickness(18, 6, 18, 6), Cursor = Cursors.Hand, FontSize = 13 };
        var t = ThemeManager.Current;
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.Name = "Bd";
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
        bd.SetValue(Border.BackgroundProperty, primary ? t.Accent : new SolidColorBrush(Color.FromArgb(0x30, 0x80, 0x80, 0x88)));
        bd.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        bd.AppendChild(cp);
        var tpl = new ControlTemplate(typeof(Button)) { VisualTree = bd };
        var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        over.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85, "Bd"));
        tpl.Triggers.Add(over);
        var press = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        press.Setters.Add(new Setter(UIElement.OpacityProperty, 0.65, "Bd"));
        tpl.Triggers.Add(press);
        b.Template = tpl;
        if (primary) b.Foreground = Brushes.White; else b.SetResourceReference(ForegroundProperty, "Md.PopupFg");
        return b;
    }
}
