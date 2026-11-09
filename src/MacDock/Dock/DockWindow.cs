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
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        UseLayoutRounding = false;
        _idle = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
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
            Native.SetWindowPos(Hwnd, IntPtr.Zero, x, y, w, h, Native.SWP_NOACTIVATE | Native.SWP_NOZORDER);
            View.ApplySettings();
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
        DockModel.Instance.Invalidated -= OnModelInvalidated;
        DockModel.Instance.ItemsChanged -= OnItemsChanged;
        try { _appBar?.Unregister(); } catch { }
        Close();
    }
}
