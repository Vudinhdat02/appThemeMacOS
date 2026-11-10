using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MacDock;

/// <summary>Cửa sổ trong suốt chứa Dock cho một màn hình.</summary>
public sealed class DockWindow : Window
{
    public MonitorInfo Monitor { get; private set; }
    public DockView View { get; }
    public IntPtr Hwnd { get; private set; }
    AppBar? _appBar;
    readonly DispatcherTimer _idle;
    bool _fullscreenHidden;
    DateTime _lastLayout;
    bool _inLayout;

    public DockWindow(MonitorInfo monitor)
    {
        Monitor = monitor;
        Title = "MacDock Dock";
        WindowStyle = WindowStyle.None;
        // Mặc định: cửa sổ được DWM ghép trong suốt trực tiếp (không phải cửa sổ "layered").
        // Cửa sổ layered của WPF phải chép cả khung hình qua CPU mỗi lần vẽ nên hiệu ứng phóng to chỉ được ~30-40 khung/giây.
        Composited = Environment.GetEnvironmentVariable("MD_LAYERED") != "1";
        AllowsTransparency = !Composited;
        Background = Brushes.Transparent;
        if (Composited)
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome { CaptionHeight = 0, GlassFrameThickness = new Thickness(-1), ResizeBorderThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false });
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        UseLayoutRounding = false;
        _idle = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(30) };
        View = new DockView(this);
        Content = View;
        _idle.Tick += (_, _) => View.IdleCheck();
        DockModel.Instance.Invalidated += OnModelInvalidated;
        DockModel.Instance.ItemsChanged += OnItemsChanged;
        DpiChanged += (_, _) => { if (!_inLayout) Dispatcher.BeginInvoke(() => LayoutWindow(), DispatcherPriority.Background); };
    }

    void OnModelInvalidated()
    {
        if (Log.Verbose) Diag.Hit("invalidated");
        View.InvalidateVisual();
        if (DockModel.Instance.Items.Any(i => i.BounceStart >= 0)) View.Wake();
    }
    void OnItemsChanged() => Dispatcher.BeginInvoke(() => LayoutWindow(), DispatcherPriority.Background);

    public void StartIdle() { if (!_idle.IsEnabled) _idle.Start(); }
    public void StopIdle() => _idle.Stop();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Hwnd = new WindowInteropHelper(this).Handle;
        Native.AddExStyle(Hwnd, Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
        int pref = 1; // DWMWCP_DONOTROUND
        Native.DwmSetWindowAttribute(Hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4);
        if (Composited)
        {
            if (HwndSource.FromHwnd(Hwnd)?.CompositionTarget is { } ct) ct.BackgroundColor = Colors.Transparent;
            int none = unchecked((int)0xFFFFFFFE); // không viền DWM
            Native.DwmSetWindowAttribute(Hwnd, Native.DWMWA_BORDER_COLOR, ref none, 4);
        }
        int excl = 1;
        Native.DwmSetWindowAttribute(Hwnd, Native.DWMWA_EXCLUDED_FROM_PEEK, ref excl, 4);
        HwndSource.FromHwnd(Hwnd)?.AddHook(WndProc);
        _appBar = new AppBar(Hwnd);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_MOUSEACTIVATE) { handled = true; return new IntPtr(Native.MA_NOACTIVATE); }
        if (_appBar != null && msg == (int)_appBar.CallbackMessage)
        {
            uint code = (uint)wParam.ToInt64();
            if (code == Native.ABN_POSCHANGED && (DateTime.Now - _lastLayout).TotalMilliseconds > 500)
                Dispatcher.BeginInvoke(() => LayoutWindow(), DispatcherPriority.Background);
            else if (code == Native.ABN_FULLSCREENAPP)
                App.Instance.CheckFullscreen();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public bool ReservationLost()
    {
        var cfg = Config.Current.Dock;
        if (!cfg.ReserveSpace || cfg.AutoHide || _fullscreenHidden || Hwnd == IntPtr.Zero || !IsVisible) return false;
        var wa = Monitors.WorkAreaOf(Monitor.Handle);
        var b = Monitor.Bounds;
        int t = (int)(View.ReserveThickness * Monitor.Scale) - 2;
        return cfg.Edge switch
        {
            DockEdge.Left => wa.Left < b.Left + t,
            DockEdge.Right => wa.Right > b.Right - t,
            _ => wa.Bottom > b.Bottom - t
        };
    }

    public bool Composited { get; }
    string _rgnKey = "";

    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern int CombineRgn(IntPtr dst, IntPtr a, IntPtr b, int mode);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);

    /// <summary>
    /// Vùng nhận chuột của cửa sổ (đơn vị DIP của Dock). null = cả cửa sổ. Cửa sổ ghép bằng DWM không tự cho chuột
    /// xuyên qua chỗ trong suốt, nên khi không rê chuột trên Dock chỉ giữ phần nền Dock để bấm được vào app bên dưới.
    /// </summary>
    public void SetHitRegion(IReadOnlyList<Rect>? dip)
    {
        if (!Composited || Hwnd == IntPtr.Zero) return;
        double sc = Monitor.Scale;
        string key = dip == null ? "all" : string.Join(";", dip.Select(r => $"{Math.Floor(r.Left * sc)},{Math.Floor(r.Top * sc)},{Math.Ceiling(r.Right * sc)},{Math.Ceiling(r.Bottom * sc)}"));
        if (key == _rgnKey) return;
        _rgnKey = key;
        if (dip == null) { SetWindowRgn(Hwnd, IntPtr.Zero, false); return; }
        IntPtr rgn = CreateRectRgn(0, 0, 0, 0);
        foreach (var r in dip)
        {
            var one = CreateRectRgn((int)Math.Floor(r.Left * sc), (int)Math.Floor(r.Top * sc), (int)Math.Ceiling(r.Right * sc), (int)Math.Ceiling(r.Bottom * sc));
            CombineRgn(rgn, rgn, one, 2 /*RGN_OR*/);
            DeleteObject(one);
        }
        if (SetWindowRgn(Hwnd, rgn, false) == 0) DeleteObject(rgn);
    }

    public void ExplorerRestarted() { _appBar?.Reregister(); _lastKey = ""; LayoutWindow(true); BringTopmost(); }

    public void SetMonitor(MonitorInfo m) { Monitor = m; LayoutWindow(); }

    public void Init()
    {
        new WindowInteropHelper(this).EnsureHandle();
        LayoutWindow();
        Show();
        LayoutWindow();
        StartIdle();
    }

    /// <summary>Tính vị trí cửa sổ (pixel vật lý) và chừa chỗ AppBar nếu cần.</summary>
    string _lastKey = "";
    RECT _base;
    double _offsetDip;
    DispatcherTimer? _coverTimer;

    (int, int) OffsetPx(double dip)
    {
        int d = (int)Math.Round(dip * Monitor.Scale);
        return Config.Current.Dock.Edge switch { DockEdge.Left => (-d, 0), DockEdge.Right => (d, 0), _ => (0, d) };
    }

    /// <summary>Dịch cửa sổ ra ngoài mép màn hình (tự ẩn) — chỉ di chuyển, không vẽ lại.</summary>
    public void SetHideOffset(double dip)
    {
        _offsetDip = dip;
        if (Hwnd == IntPtr.Zero || _base.Width == 0) return;
        var (ox, oy) = OffsetPx(dip);
        Native.SetWindowPos(Hwnd, IntPtr.Zero, _base.Left + ox, _base.Top + oy, 0, 0, Native.SWP_NOACTIVATE | Native.SWP_NOZORDER | Native.SWP_NOSIZE);
    }

    /// <summary>Kiểm tra định kỳ (khi bật tự ẩn) xem có cửa sổ nào che vùng Dock.</summary>
    void UpdateCoverTimer()
    {
        bool need = Config.Current.Dock.AutoHide && Config.Current.Dock.AutoHideOnlyWhenCovered;
        if (need && _coverTimer == null)
        {
            _coverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _coverTimer.Tick += (_, _) => View.SetCovered(IsDockCovered());
            _coverTimer.Start();
            View.SetCovered(IsDockCovered());
        }
        else if (!need && _coverTimer != null) { _coverTimer.Stop(); _coverTimer = null; }
    }

    bool IsDockCovered()
    {
        var pr = View.PlateScreenRect();
        if (pr.IsEmpty) return false;
        var (ox, oy) = OffsetPx(_offsetDip);
        var band = new RECT((int)pr.Left - ox, (int)pr.Top - oy, (int)pr.Right - ox, (int)pr.Bottom - oy);
        foreach (var w in WindowTracker.Instance.Windows)
        {
            if (w.Minimized || !Native.IsWindowVisible(w.Hwnd) || Native.IsIconic(w.Hwnd)) continue;
            if (Native.DwmGetWindowAttribute(w.Hwnd, 9 /*EXTENDED_FRAME_BOUNDS*/, out RECT r, System.Runtime.InteropServices.Marshal.SizeOf<RECT>()) != 0 && !Native.GetWindowRect(w.Hwnd, out r)) continue;
            if (r.Left < band.Right && r.Right > band.Left && r.Top < band.Bottom && r.Bottom > band.Top) { if (Log.Verbose && !View.IsCoveredFlag) Log.Debug($"Che bởi {w.Title} {r} band={band}"); return true; }
        }
        return false;
    }

    public void LayoutWindow(bool force = false)
    {
        if (Hwnd == IntPtr.Zero || _inLayout) return;
        _inLayout = true;
        try
        {
            var cfg = Config.Current.Dock;
            var m = Monitors.FromHandle(Monitor.Handle) ?? Monitor;
            Monitor = m;
            double scale = m.Scale;
            int cross = (int)Math.Ceiling(View.ComputeCross() * scale);
            int main = (int)Math.Ceiling(View.ComputeMain() * scale);
            RECT b = m.Bounds;
            bool horizontal = cfg.Edge == DockEdge.Bottom;
            main = Math.Min(main, horizontal ? b.Width : b.Height);

            uint abe = cfg.Edge switch { DockEdge.Left => Native.ABE_LEFT, DockEdge.Right => Native.ABE_RIGHT, _ => Native.ABE_BOTTOM };
            RECT anchor;
            if (cfg.ReserveSpace && !cfg.AutoHide && !_fullscreenHidden)
            {
                int thick = (int)Math.Ceiling(View.ReserveThickness * scale);
                anchor = _appBar!.SetPosition(abe, TaskbarController.IsHidden ? b : b, thick);
            }
            else
            {
                _appBar?.Unregister();
                anchor = TaskbarController.IsHidden ? b : Monitors.WorkAreaOf(m.Handle);
                if (TaskbarController.IsHidden)
                {
                    // vẫn tránh thanh Menu Bar ở cạnh trên khi Dock nằm dọc
                    var wa = Monitors.WorkAreaOf(m.Handle);
                    anchor.Top = Math.Max(anchor.Top, wa.Top);
                }
            }

            int x, y, w, h;
            if (horizontal)
            {
                w = main; h = cross;
                x = b.Left + (b.Width - w) / 2;
                y = anchor.Bottom - h;
            }
            else
            {
                w = cross; h = main;
                var wa = Monitors.WorkAreaOf(m.Handle);
                int top = Math.Max(b.Top, wa.Top);
                y = top + (b.Bottom - top - h) / 2;
                x = cfg.Edge == DockEdge.Left ? anchor.Left : anchor.Right - w;
            }
            string key = $"{x},{y},{w},{h},{cfg.Edge},{cfg.Shadow},{cfg.AutoHide},{ThemeManager.Current?.Def.Id},{ThemeManager.Current?.IsDark},{App.EffectsOff}";
            _lastLayout = DateTime.Now;
            if (key == _lastKey && !force) return;
            _lastKey = key;
            _base = new RECT(x, y, x + w, y + h);
            var (ox, oy) = OffsetPx(_offsetDip);
            Native.SetWindowPos(Hwnd, IntPtr.Zero, x + ox, y + oy, w, h, Native.SWP_NOACTIVATE | Native.SWP_NOZORDER);
            View.ApplySettings();
            UpdateCoverTimer();
        }
        catch (Exception ex) { Log.Error("Dock layout lỗi", ex); }
        finally { _inLayout = false; }
    }

    public void SetFullscreenHidden(bool hidden)
    {
        if (_fullscreenHidden == hidden) return;
        _fullscreenHidden = hidden;
        if (hidden) { Hide(); StopIdle(); }
        else { Show(); LayoutWindow(); BringTopmost(); StartIdle(); }
    }

    public void BringTopmost()
    {
        if (Hwnd != IntPtr.Zero && !_fullscreenHidden)
            Native.SetWindowPos(Hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    public void Shutdown()
    {
        _idle.Stop();
        _coverTimer?.Stop();
        DockModel.Instance.Invalidated -= OnModelInvalidated;
        DockModel.Instance.ItemsChanged -= OnItemsChanged;
        try { _appBar?.Unregister(); } catch { }
        Close();
    }
}
