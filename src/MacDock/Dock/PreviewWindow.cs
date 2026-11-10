using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MacDock;

/// <summary>Ô xem trước các cửa sổ của một ứng dụng (ảnh thu nhỏ trực tiếp của DWM).</summary>
public sealed class PreviewWindow : Window
{
    [StructLayout(LayoutKind.Sequential)]
    struct DWM_THUMBNAIL_PROPERTIES
    {
        public int dwFlags;
        public RECT rcDestination, rcSource;
        public byte opacity;
        public int fVisible, fSourceClientAreaOnly;
    }
    [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref DWM_THUMBNAIL_PROPERTIES p);
    [DllImport("dwmapi.dll")] static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out SIZE size);
    const int DWM_TNP_RECTDESTINATION = 1, DWM_TNP_OPACITY = 4, DWM_TNP_VISIBLE = 8, DWM_TNP_SOURCECLIENTAREAONLY = 0x10;

    static PreviewWindow? _inst;
    readonly List<(IntPtr thumb, Border slot, IntPtr src)> _thumbs = new();
    readonly StackPanel _row = new() { Orientation = Orientation.Horizontal };
    readonly DispatcherTimer _watch;
    IntPtr _hwnd;
    public DockItem? Item { get; private set; }
    DockWindow? _dock;
    Rect _anchor;
    DateTime _outside = DateTime.MaxValue;

    public static bool AnyOpen => _inst != null && _inst.IsVisible;
    public static bool IsOpenFor(DockItem item) => _inst?.Item == item && _inst.IsVisible;
    public static bool ContainsCursor() => _inst != null && _inst.IsVisible && _inst.CursorInside();
    /// <summary>Ô xem trước đang mở và chuột nằm trong nó hoặc trên đường từ icon tới nó → Dock phải giữ nguyên.</summary>
    public static bool HoldsDock()
    {
        if (_inst == null || !_inst.IsVisible || _inst._hwnd == IntPtr.Zero) return false;
        Native.GetCursorPos(out var pt);
        return _inst.CursorInside() || _inst.BetweenIconAndPreview(pt);
    }

    PreviewWindow()
    {
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        var t = ThemeManager.Current;
        Background = t.PopupBackground;
        SetResourceReference(ForegroundProperty, "Md.PopupFg");
        SetResourceReference(FontFamilyProperty, "Md.Font");
        FontSize = 12;
        Content = new Border { Child = _row, Padding = new Thickness(8) };
        _watch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _watch.Tick += (_, _) => Watch();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        Native.AddExStyle(_hwnd, Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
        int round = 2; Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
        int dark = ThemeManager.Current.IsDark ? 1 : 0; Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
        HwndSource.FromHwnd(_hwnd)?.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
        {
            if (msg == Native.WM_MOUSEACTIVATE) { handled = true; return new IntPtr(Native.MA_NOACTIVATE); }
            return IntPtr.Zero;
        });
    }

    /// <summary>Hiện xem trước cho <paramref name="item"/>; <paramref name="iconScreen"/> là vị trí icon (pixel màn hình).</summary>
    public static void ShowFor(DockWindow dock, DockItem item, Rect iconScreen)
    {
        if (!Config.Current.Dock.WindowPreviews || item.Windows.Count == 0) { HideNow(); return; }
        if (IsOpenFor(item)) return;
        HideNow();
        _inst = new PreviewWindow();
        _inst.Build(dock, item, iconScreen);
    }

    public static void HideNow()
    {
        if (_inst == null) return;
        var w = _inst; _inst = null;
        w._watch.Stop();
        foreach (var t in w._thumbs) DwmUnregisterThumbnail(t.thumb);
        w._thumbs.Clear();
        w.Close();
    }

    void Build(DockWindow dock, DockItem item, Rect anchor)
    {
        Item = item; _dock = dock; _anchor = anchor;
        double tw = Math.Clamp(Config.Current.Dock.PreviewWidth, 120, 360), th = tw * 0.62;
        var t = ThemeManager.Current;
        foreach (var w in item.Windows.OrderBy(x => x.Hwnd.ToInt64()).Take(8))
        {
            var win = w;
            var slot = new Border { Width = tw, Height = th, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromArgb(0x18, 0x80, 0x80, 0x88)) };
            var icon = DockModel.Instance.GetIcon(item);
            if (icon != null) slot.Child = new Image { Source = icon, Width = 40, Height = 40, Opacity = 0.5 };
            var title = new TextBlock { Text = string.IsNullOrWhiteSpace(w.Title) ? item.Name : w.Title, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = tw - 24, VerticalAlignment = VerticalAlignment.Center };
            var close = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x88)), Cursor = Cursors.Hand, ToolTip = L.T("Đóng cửa sổ"),
                Child = new System.Windows.Shapes.Path { Data = Geometry.Parse("M4.5,4.5 L11.5,11.5 M11.5,4.5 L4.5,11.5"), Stroke = t.PopupForeground, StrokeThickness = 1.2, Width = 16, Height = 16 } };
            close.MouseLeftButtonUp += (_, e) => { e.Handled = true; WindowActions.Close(win.Hwnd); HideNow(); };
            var head = new DockPanel { Margin = new Thickness(2, 0, 2, 5) };
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close); head.Children.Add(title);
            var cell = new StackPanel { Margin = new Thickness(4) };
            cell.Children.Add(head); cell.Children.Add(slot);
            var cb = new Border { Child = cell, CornerRadius = new CornerRadius(8), Padding = new Thickness(4), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            cb.MouseEnter += (_, _) => cb.Background = new SolidColorBrush(Color.FromArgb(0x28, 0x80, 0x80, 0x88));
            cb.MouseLeave += (_, _) => cb.Background = Brushes.Transparent;
            cb.MouseLeftButtonUp += (_, _) => { WindowActions.Activate(win.Hwnd); HideNow(); };
            _row.Children.Add(cb);
            _thumbs.Add((IntPtr.Zero, slot, w.Hwnd));
        }
        Left = -10000; Top = -10000;
        Show();
        UpdateLayout();
        Position();
        Dispatcher.BeginInvoke(RegisterThumbs, DispatcherPriority.Loaded);
        _watch.Start();
    }

    void Position()
    {
        var m = _dock!.Monitor;
        double s = m.Scale;
        double w = ActualWidth * s, h = ActualHeight * s;
        double cx = _anchor.Left + _anchor.Width / 2, x, y;
        switch (Config.Current.Dock.Edge)
        {
            case DockEdge.Left: x = _anchor.Right + 10 * s; y = _anchor.Top + _anchor.Height / 2 - h / 2; break;
            case DockEdge.Right: x = _anchor.Left - 10 * s - w; y = _anchor.Top + _anchor.Height / 2 - h / 2; break;
            default: x = cx - w / 2; y = _anchor.Top - 14 * s - h; break;
        }
        x = Math.Clamp(x, m.Bounds.Left + 4, m.Bounds.Right - w - 4);
        y = Math.Clamp(y, m.Bounds.Top + 4, m.Bounds.Bottom - h - 4);
        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, (int)x, (int)y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    void RegisterThumbs()
    {
        double s = _dock!.Monitor.Scale;
        for (int i = 0; i < _thumbs.Count; i++)
        {
            var (old, slot, src) = _thumbs[i];
            if (old != IntPtr.Zero) continue;
            if (DwmRegisterThumbnail(_hwnd, src, out IntPtr th) != 0) continue;
            DwmQueryThumbnailSourceSize(th, out SIZE size);
            var tl = slot.TranslatePoint(new Point(0, 0), this);
            double sw = slot.ActualWidth, sh = slot.ActualHeight;
            double ar = size.cy > 0 ? (double)size.cx / size.cy : 1.6;
            double dw = sw, dh = sw / ar;
            if (dh > sh) { dh = sh; dw = sh * ar; }
            double dx = tl.X + (sw - dw) / 2, dy = tl.Y + (sh - dh) / 2;
            var p = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_VISIBLE | DWM_TNP_OPACITY | DWM_TNP_SOURCECLIENTAREAONLY,
                rcDestination = new RECT((int)(dx * s), (int)(dy * s), (int)((dx + dw) * s), (int)((dy + dh) * s)),
                opacity = 255, fVisible = 1, fSourceClientAreaOnly = 0
            };
            DwmUpdateThumbnailProperties(th, ref p);
            if (size.cx > 0 && slot.Child is Image im) im.Visibility = Visibility.Hidden;
            _thumbs[i] = (th, slot, src);
        }
    }

    bool CursorInside()
    {
        Native.GetCursorPos(out var pt);
        Native.GetWindowRect(_hwnd, out var r);
        r.Left -= 6; r.Top -= 6; r.Right += 6; r.Bottom += 20;
        return r.Contains(pt.X, pt.Y);
    }

    void Watch()
    {
        Native.GetCursorPos(out var pt);
        bool overIcon = _anchor.Contains(new Point(pt.X, pt.Y)) || BetweenIconAndPreview(pt);
        if (CursorInside() || overIcon) { _outside = DateTime.MaxValue; return; }
        if (_outside == DateTime.MaxValue) _outside = DateTime.Now;
        if ((DateTime.Now - _outside).TotalMilliseconds > 250) HideNow();
    }

    bool BetweenIconAndPreview(POINT pt)
    {
        Native.GetWindowRect(_hwnd, out var r);
        var u = Rect.Union(_anchor, new Rect(r.Left, r.Top, r.Width, r.Height));
        return u.Contains(new Point(pt.X, pt.Y)) && pt.X >= Math.Min(_anchor.Left, r.Left) && pt.X <= Math.Max(_anchor.Right, r.Right)
            && (Config.Current.Dock.Edge != DockEdge.Bottom || (pt.X >= _anchor.Left - 20 && pt.X <= _anchor.Right + 20) || r.Contains(pt.X, pt.Y));
    }
}
