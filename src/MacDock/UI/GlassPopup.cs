using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace MacDock;

/// <summary>
/// Bảng nổi kiểu "liquid glass": cửa sổ không viền, không lấy focus, làm mờ thật phần phía sau (accent blur của DWM,
/// hoạt động cả khi cửa sổ không được kích hoạt), bo góc lớn của Windows 11, viền phản sáng mảnh.
/// Tự đóng khi bấm ra ngoài hoặc nhấn Esc.
/// </summary>
public class GlassPopup : Window
{
    readonly Border _frame;
    readonly ContentControl _host = new();
    IntPtr _hwnd;
    Action? _unwatch;
    public static GlassPopup? Current { get; private set; }
    /// <summary>Cú bấm mà hàm này trả về true không làm bảng đóng (vd bấm lại nút đã mở bảng — nút tự xử lý bật/tắt).</summary>
    public Func<bool>? IgnoreClick { get; set; }

    public UIElement? Body { get => _host.Content as UIElement; set => _host.Content = value; }

    public GlassPopup()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = Brushes.Transparent;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, GlassFrameThickness = new Thickness(-1), ResizeBorderThickness = new Thickness(0), UseAeroCaptionButtons = false });
        var t = ThemeManager.Current;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 13;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Foreground = Glass.Fg;
        // lớp phủ mỏng + viền phản sáng phía trên (kính)
        _frame = new Border
        {
            Child = _host,
            Background = Glass.PanelTint,
            BorderBrush = Glass.Rim,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(Glass.Radius),
            Padding = new Thickness(12),
        };
        TextElement.SetForeground(_frame, Glass.Fg);
        Content = _frame;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Closed += (_, _) => { _unwatch?.Invoke(); if (Current == this) Current = null; };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        Native.AddExStyle(_hwnd, Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
        var src = HwndSource.FromHwnd(_hwnd);
        if (src?.CompositionTarget != null) src.CompositionTarget.BackgroundColor = Colors.Transparent;
        src?.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
        {
            if (msg == Native.WM_MOUSEACTIVATE) { handled = true; return new IntPtr(Native.MA_NOACTIVATE); }
            return IntPtr.Zero;
        });
        int round = 2; Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
        int dark = Glass.Dark ? 1 : 0; Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
        // Acrylic (làm mờ + màu phủ rất nhẹ): không phụ thuộc trạng thái kích hoạt như system backdrop
        Native.SetAccent(_hwnd, 4, Glass.AccentTint);
    }

    /// <summary>Hiện bảng, căn mép phải với <paramref name="anchorRightPx"/>, mép trên tại <paramref name="topPx"/> (pixel màn hình).</summary>
    public void ShowAt(MonitorInfo mon, int anchorRightPx, int topPx)
    {
        Current?.Close();
        Current = this;
        Left = -20000; Top = -20000;
        Show();
        UpdateLayout();
        Reposition(mon, anchorRightPx, topPx);
        SizeChanged += (_, _) => Reposition(mon, anchorRightPx, topPx);
        _unwatch = OutsideClick.Watch(() => { if (IsLoaded) Close(); }, root => root == _hwnd || IsOwnModal(root) || IgnoreClick?.Invoke() == true);
    }

    void Reposition(MonitorInfo mon, int anchorRightPx, int topPx)
    {
        if (_hwnd == IntPtr.Zero) return;
        Native.GetWindowRect(_hwnd, out var r);
        int w = r.Width;
        int x = Math.Clamp(anchorRightPx - w, mon.Bounds.Left + 6, mon.Bounds.Right - w - 6);
        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, x, topPx, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    // hộp thoại của chính MacDock mở từ bảng (vd nhập mật khẩu Wi-Fi) không làm đóng bảng
    static bool IsOwnModal(IntPtr root)
    {
        Native.GetWindowThreadProcessId(root, out uint pid);
        return pid == Environment.ProcessId && Native.GetClass(root).StartsWith("HwndWrapper") && Application.Current.Windows.OfType<Window>().Any(w => w is GlassWindow && new WindowInteropHelper(w).Handle == root);
    }
}

/// <summary>Bảng màu "liquid glass" dùng chung theo chế độ sáng/tối.</summary>
public static class Glass
{
    public static bool Dark => ThemeManager.Current?.IsDark == true;
    public const double Radius = 22;

    static Brush F(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    static Brush G(Color a, Color b) { var g = new LinearGradientBrush(a, b, 90); g.Freeze(); return g; }

    public static uint AccentTint => Dark ? 0x40141416u : 0x30F2F2F2u; // AABBGGRR
    public static Brush PanelTint => Dark ? F(Color.FromArgb(0x55, 0x1C, 0x1C, 0x20)) : F(Color.FromArgb(0x66, 0xF7, 0xF7, 0xF9));
    public static Brush Rim => Dark ? G(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)) : G(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
    public static Brush Fg => Dark ? F(Color.FromRgb(0xF5, 0xF5, 0xF7)) : F(Color.FromRgb(0x1D, 0x1D, 0x1F));
    public static Brush SubFg => Dark ? F(Color.FromArgb(0x99, 0xF5, 0xF5, 0xF7)) : F(Color.FromArgb(0x8C, 0x1D, 0x1D, 0x1F));
    /// <summary>Nền các khối module (kính dày hơn nền bảng).</summary>
    public static Brush Tile => Dark ? F(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)) : F(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));
    public static Brush TileHover => Dark ? F(Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF)) : F(Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF));
    public static Brush TileRim => Dark ? G(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF)) : G(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF));
    /// <summary>Nút tròn khi tắt.</summary>
    public static Brush Off => Dark ? F(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)) : F(Color.FromArgb(0x1C, 0x00, 0x00, 0x00));
    public static Brush On => ThemeManager.Current?.Accent ?? F(Color.FromRgb(0x0A, 0x84, 0xFF));
    public static Brush Track => Dark ? F(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)) : F(Color.FromArgb(0x22, 0x00, 0x00, 0x00));
    public static Brush SliderFill => F(Color.FromRgb(0xFF, 0xFF, 0xFF));
    public static Brush Separator => Dark ? F(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)) : F(Color.FromArgb(0x1E, 0x00, 0x00, 0x00));

    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    /// <summary>Icon nét đơn từ phông Segoe Fluent Icons có sẵn của Windows 11.</summary>
    public static TextBlock Icon(string glyph, double size, Brush? fg = null) => new()
    {
        Text = glyph, FontFamily = IconFont, FontSize = size, Foreground = fg ?? Fg,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Center
    };
}
