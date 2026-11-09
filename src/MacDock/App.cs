using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MacDock;

public sealed class App : Application
{
    public static App Instance { get; private set; } = null!;
    public static bool RestartRequested { get; private set; }

    public void Restart() { RestartRequested = true; Quit(); }
    public static string VersionString => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.2.0";

    /// <summary>Tắt hiệu ứng hoàn toàn (người dùng chọn, hoặc đang dùng pin khi bật giảm hiệu ứng).</summary>
    public static bool EffectsOff => Config.Current.Performance.DisableAnimations;
    public static bool ReducedEffects => Config.Current.Performance.ReduceOnBattery && StatusService.Instance.OnBattery;
    public static int EffectiveFps
    {
        get
        {
            int f = Config.Current.Performance.FpsLimit;
            if (ReducedEffects) f = f <= 0 ? 30 : Math.Min(f, 30);
            return f;
        }
    }

    readonly List<DockWindow> _docks = new();
    readonly List<MenuBarWindow> _bars = new();
    HwndSource? _msgWindow;
    uint _wmShellHook, _wmTaskbarCreated, _wmOpenSettings, _wmQuit, _wmTest;
    DispatcherTimer? _maintenance, _displayDebounce;
    SettingsWindow? _settings;
    bool _shellStarted, _quitting;
    static int _cleanedUp;
    int _errorCount;
    DateTime _errorWindow = DateTime.Now;

    public App()
    {
        Instance = this;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
        {
            Log.Error("UnhandledException", a.ExceptionObject as Exception);
            EmergencyCleanup();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => EmergencyCleanup();
        DispatcherUnhandledException += OnDispatcherError;
        TaskScheduler.UnobservedTaskException += (_, a) => { Log.Error("Task lỗi", a.Exception); a.SetObserved(); };
        SessionEnding += (_, _) => EmergencyCleanup();

        Log.Info($"MacDock {VersionString} khởi động (pid {Environment.ProcessId}, dữ liệu: {Paths.Root})");

        // Phiên trước bị tắt đột ngột? Khôi phục taskbar trước.
        if (TaskbarController.RecoverFromCrash()) Log.Warn("Đã khôi phục taskbar từ phiên trước bị dừng đột ngột.");

        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MacDock;component/UI/Styles.xaml") });
        Config.Load();
        ThemeManager.Apply();
        IconCache.IconPixelSize = Config.Current.Dock.IconSize * Config.Current.Dock.MagnifyScale * MaxScale() > 128 ? 256 : 128;
        IconCache.Init(Dispatcher);

        CreateMessageWindow();
        WindowTracker.Instance.Start();
        StatusService.Instance.Start(Dispatcher);
        DockModel.Instance.Start();
        Config.Changed += OnConfigChanged;
        ThemeManager.Changed += OnThemeChanged;
        WindowTracker.Instance.ForegroundChanged += OnForegroundChanged;
        TrayService.Instance.Changed += () => { foreach (var b in _bars) b.RebuildTray(); };
        TrayService.Instance.IconRectProvider = icon => _bars.Select(b => b.TrayIconRect(icon)).FirstOrDefault(r => r != null);
        StatusService.Instance.Changed += () => { foreach (var b in _bars) b.RebuildRight(); };
        StatusService.Instance.ClockChanged += () => { foreach (var b in _bars) b.UpdateClock(); };

        if (Config.IsFirstRun)
        {
            var w = new WelcomeWindow();
            w.Closed += (_, _) => { if (!_quitting) StartShell(); };
            w.Show();
        }
        else
        {
            StartShell();
            if (Program.OpenSettingsOnStart) ShowSettings();
        }

        // Ghi lại thời gian khởi động, rồi mới làm các việc chậm (WMI, theme mẫu…) sau khi giao diện đã hiện
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var since = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
                Log.Info($"Giao diện đã hiện sau {since.TotalMilliseconds:0} ms kể từ khi tiến trình bắt đầu");
            }
            catch { }
            MediaKeys.Apply(Dispatcher);
            Task.Run(ThemeManager.WriteSamples);
        }, DispatcherPriority.ApplicationIdle);
    }

    static double MaxScale()
    {
        try
        {
            return Monitors.GetAll().Select(m => m.Scale).DefaultIfEmpty(1).Max();
        }
        catch { return 1; }
    }

    void OnDispatcherError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Lỗi giao diện", e.Exception);
        if ((DateTime.Now - _errorWindow).TotalMinutes > 1) { _errorWindow = DateTime.Now; _errorCount = 0; }
        if (++_errorCount > 30)
        {
            Log.Error("Quá nhiều lỗi liên tiếp, thoát để bảo vệ hệ thống.");
            e.Handled = true;
            Quit();
            return;
        }
        e.Handled = true;
    }

    // ------------------------------------------------------------------ cửa sổ thông điệp ẩn

    void CreateMessageWindow()
    {
        var p = new HwndSourceParameters(Program.MessagingTitle)
        {
            Width = 0, Height = 0, PositionX = -32000, PositionY = -32000,
            WindowStyle = unchecked((int)0x80000000), // WS_POPUP, không hiện
            ExtendedWindowStyle = (int)Native.WS_EX_TOOLWINDOW,
        };
        _msgWindow = new HwndSource(p);
        _msgWindow.AddHook(MsgProc);
        _wmShellHook = Native.RegisterWindowMessage("SHELLHOOK");
        _wmTaskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        _wmOpenSettings = Native.RegisterWindowMessage("MacDock.OpenSettings");
        _wmQuit = Native.RegisterWindowMessage("MacDock.Quit");
        _wmTest = Program.TestMode ? Native.RegisterWindowMessage("MacDock.Test") : uint.MaxValue;
        foreach (var m in new[] { _wmOpenSettings, _wmQuit, _wmTaskbarCreated })
            Native.ChangeWindowMessageFilterEx(_msgWindow.Handle, m, Native.MSGFLT_ALLOW, IntPtr.Zero);
        if (!Native.RegisterShellHookWindow(_msgWindow.Handle)) Log.Warn("RegisterShellHookWindow thất bại");
    }

    IntPtr MsgProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        uint m = (uint)msg;
        if (m == _wmShellHook) { WindowTracker.Instance.OnShellHook(wParam.ToInt32(), lParam); return IntPtr.Zero; }
        if (m == _wmOpenSettings) { Dispatcher.BeginInvoke(() => ShowSettings()); handled = true; return IntPtr.Zero; }
        if (m == _wmQuit) { Dispatcher.BeginInvoke(Quit); handled = true; return IntPtr.Zero; }
        if (m == _wmTest) { int cmd = wParam.ToInt32(); long arg = lParam.ToInt64(); Dispatcher.BeginInvoke(() => RunTest(cmd, arg)); handled = true; return new IntPtr(1); }
        if (m == _wmTaskbarCreated)
        {
            // Explorer khởi động lại
            Log.Info("Explorer đã khởi động lại (TaskbarCreated)");
            Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(1500);
                if (_shellStarted && Config.Current.General.HideTaskbar) TaskbarController.EnsureHidden();
                RelayoutAll();
            });
            return IntPtr.Zero;
        }
        switch (msg)
        {
            case Native.WM_DISPLAYCHANGE:
                ScheduleDisplayRebuild();
                break;
            case Native.WM_SETTINGCHANGE:
                string? area = lParam != IntPtr.Zero ? Marshal.PtrToStringUni(lParam) : null;
                if (area == "ImmersiveColorSet") Dispatcher.BeginInvoke(() => ThemeManager.Apply(false));
                if (wParam.ToInt64() == 0x14 /* SPI_SETDESKWALLPAPER */)
                    Dispatcher.BeginInvoke(() => { Wallpaper.Invalidate(); foreach (var b in _bars) { b.ApplyTheme(); b.RebuildLeft(); b.RebuildRight(); } });
                if (wParam.ToInt64() == 0x2F /* SPI_SETWORKAREA */ || area == "TraySettings") ScheduleDisplayRebuild();
                break;
            case Native.WM_POWERBROADCAST:
                Dispatcher.BeginInvoke(() => StatusService.Instance.RefreshAll());
                break;
        }
        return IntPtr.Zero;
    }

    // ------------------------------------------------------------------ vỏ (shell)

    void StartShell()
    {
        if (_shellStarted) return;
        _shellStarted = true;
        var g = Config.Current.General;
        Autostart.Apply(g.AutoStart);
        if (g.Watchdog) Dispatcher.BeginInvoke(async () => { await Task.Delay(4000); if (!_quitting) Program.StartWatchdog(); });
        ApplyTaskbar();
        BuildWindows();
        _maintenance = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _maintenance.Tick += (_, _) => Maintenance();
        _maintenance.Start();
        if (Config.Current.Performance.TrimMemory)
            Dispatcher.BeginInvoke(async () => { await Task.Delay(8000); TrimMemory(); });
    }

    void ApplyTaskbar()
    {
        var g = Config.Current.General;
        if (g.HideTaskbar)
        {
            TaskbarController.Hide();
            if (Config.Current.MenuBar.Enabled && Config.Current.MenuBar.ShowTray) TrayService.Instance.Start(Dispatcher);
            else TrayService.Instance.Stop();
        }
        else
        {
            TrayService.Instance.Stop();
            TaskbarController.Restore();
        }
    }

    void BuildWindows()
    {
        foreach (var d in _docks) d.Shutdown();
        foreach (var b in _bars) b.Shutdown();
        _docks.Clear(); _bars.Clear();
        var monitors = Monitors.GetAll();
        Log.Info("Màn hình: " + string.Join("; ", monitors));
        var mb = Config.Current.MenuBar;
        if (mb.Enabled)
            foreach (var m in monitors.Where(m => mb.Monitors == MonitorMode.All || m.Primary))
            {
                var w = new MenuBarWindow(m);
                w.Init();
                _bars.Add(w);
            }
        var dk = Config.Current.Dock;
        foreach (var m in monitors.Where(m => dk.Monitors == MonitorMode.All || m.Primary))
        {
            var w = new DockWindow(m);
            w.Init();
            _docks.Add(w);
        }
        CheckFullscreen();
    }

    void RelayoutAll()
    {
        foreach (var b in _bars) b.LayoutWindow();
        foreach (var d in _docks) d.LayoutWindow();
    }

    void ScheduleDisplayRebuild()
    {
        if (!_shellStarted) return;
        _displayDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _displayDebounce.Tick -= DisplayTick;
        _displayDebounce.Tick += DisplayTick;
        _displayDebounce.Stop(); _displayDebounce.Start();
    }

    void DisplayTick(object? s, EventArgs e)
    {
        _displayDebounce!.Stop();
        var now = Monitors.GetAll();
        var cur = _docks.Select(d => d.Monitor).Concat(_bars.Select(b => b.Monitor)).ToList();
        bool same = now.Count == _docks.Count && now.All(m => cur.Any(c => c.Device == m.Device && c.Bounds.ToString() == m.Bounds.ToString() && Math.Abs(c.Scale - m.Scale) < 0.01));
        if (same) RelayoutAll();
        else BuildWindows();
    }

    int _maintTicks;
    void Maintenance()
    {
        if (Log.Verbose && ++_maintTicks % 4 == 0) Log.Debug("Diag 12s: " + Diag.Snapshot());
        if (Config.Current.General.HideTaskbar) TaskbarController.EnsureHidden();
        CheckFullscreen();
    }

    public void OnPowerChanged()
    {
        Log.Info("Nguồn điện thay đổi: " + (StatusService.Instance.OnBattery ? "dùng pin" : "cắm sạc"));
        foreach (var d in _docks) d.View.ApplySettings();
    }

    /// <summary>Ẩn Dock/Menu Bar trên màn hình có ứng dụng toàn màn hình (game, video...).</summary>
    public void CheckFullscreen()
    {
        IntPtr fg = Native.GetForegroundWindow();
        IntPtr fgMon = IntPtr.Zero;
        bool full = false;
        if (fg != IntPtr.Zero)
        {
            string cls = Native.GetClass(fg);
            Native.GetWindowThreadProcessId(fg, out uint pid);
            if (pid != Environment.ProcessId && cls is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow" or "XamlExplorerHostIslandWindow")
                && Native.GetWindowRect(fg, out var r) && !Native.IsZoomed(fg) && Native.IsWindowVisible(fg))
            {
                fgMon = Native.MonitorFromWindow(fg, Native.MONITOR_DEFAULTTONEAREST);
                var b = Monitors.BoundsOf(fgMon);
                full = r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
            }
        }
        foreach (var d in _docks) d.SetFullscreenHidden(full && d.Monitor.Handle == fgMon);
        foreach (var bw in _bars) bw.SetFullscreenHidden(full && bw.Monitor.Handle == fgMon);
    }

    void OnForegroundChanged()
    {
        CheckFullscreen();
        foreach (var b in _bars) b.RebuildLeft();
    }

    void OnThemeChanged()
    {
        foreach (var b in _bars) { b.ApplyTheme(); b.RebuildLeft(); b.RebuildRight(); }
        foreach (var d in _docks) d.View.ApplySettings();
    }

    void OnConfigChanged(string section)
    {
        if (!_shellStarted) return;
        switch (section)
        {
            case "general":
                MediaKeys.Apply(Dispatcher);
                Autostart.Apply(Config.Current.General.AutoStart);
                ApplyTaskbar();
                RelayoutAll();
                foreach (var b in _bars) b.RebuildRight();
                break;
            case "dock":
            case "pins":
                if (_docks.Count != Monitors.GetAll().Count(m => Config.Current.Dock.Monitors == MonitorMode.All || m.Primary)) BuildWindows();
                else foreach (var d in _docks) d.LayoutWindow(true);
                break;
            case "menubar":
                bool want = Config.Current.MenuBar.Enabled;
                int expected = want ? Monitors.GetAll().Count(m => Config.Current.MenuBar.Monitors == MonitorMode.All || m.Primary) : 0;
                if (_bars.Count != expected) { ApplyTaskbar(); BuildWindows(); }
                else
                {
                    if (Config.Current.General.HideTaskbar && Config.Current.MenuBar.ShowTray && !TrayService.Instance.Running) TrayService.Instance.Start(Dispatcher);
                    foreach (var b in _bars) { b.ApplyTheme(); b.LayoutWindow(); b.RebuildLeft(); b.RebuildRight(); }
                    foreach (var d in _docks) d.LayoutWindow();
                }
                break;
            case "theme":
                ThemeManager.Apply();
                break;
            case "performance":
                foreach (var d in _docks) d.View.ApplySettings();
                break;
            case "all":
                ThemeManager.Apply();
                Autostart.Apply(Config.Current.General.AutoStart);
                ApplyTaskbar();
                BuildWindows();
                break;
        }
    }

    // ------------------------------------------------------------------ kiểm thử

    void RunTest(int cmd, long arg)
    {
        try
        {
            var dock = _docks.FirstOrDefault();
            var bar = _bars.FirstOrDefault();
            int lo = (int)(arg & 0xFFFF), hi = (int)((arg >> 16) & 0xFFFF);
            switch (cmd)
            {
                case 1: DockView.FakeCursor = new POINT(lo, hi); dock?.View.Wake(); break;
                case 2: DockView.FakeCursor = null; dock?.View.Wake(); break;
                case 3: if (lo < DockModel.Instance.Items.Count) DockModel.Instance.Activate(DockModel.Instance.Items[lo]); break;
                case 4: if (lo < DockModel.Instance.Items.Count) dock?.View.ShowMenu(DockModel.Instance.Items[lo]); break;
                case 5: bar?.TestOpenTop(lo); break;
                case 6: bar?.TestPopup(lo); break;
                case 7: bar?.TestClosePopups(); dock?.View.CloseMenu(); break;
                case 8: ShowSettings(new[] { "general", "dock", "menubar", "theme", "icons", "performance", "about" }[Math.Clamp(lo, 0, 6)]); break;
                case 9: Log.Info("Test tray click: " + bar?.TestTrayClick(lo, hi == 1)); break;
                case 10: if (lo < DockModel.Instance.Items.Count) DockModel.Instance.Bounce(DockModel.Instance.Items[lo], 3, 0.55); break;
                case 11:
                    if (dock != null && lo < DockModel.Instance.Items.Count)
                    {
                        var p = dock.View.ItemScreenCenter(lo);
                        Log.Info($"Test item {lo} ({DockModel.Instance.Items[lo]}) tâm = {p?.X},{p?.Y}");
                    }
                    break;
                case 12: Config.Current.Theme.ThemeId = ThemeManager.All().ElementAt(lo % ThemeManager.All().Count()).Id; Config.Current.Theme.Mode = hi == 1 ? ThemeMode.Dark : hi == 2 ? ThemeMode.Light : ThemeMode.System; Config.Touch("theme"); break;
                case 13: _settings?.Close(); break;
                case 14: Log.Info("Tray icons: " + string.Join(" ; ", TrayService.Instance.Icons.Select(i => i.ToString()))); break;
                case 15: Config.Current.Dock.Edge = (DockEdge)lo; Config.Touch("dock"); break;
                case 17: ExportShots(lo); break;
                case 18: AboutPcWindow.ShowSingle(); foreach (Window w in Windows) if (w is GlassWindow) w.Topmost = true; break;
                case 19: new WelcomeWindow(true) { Topmost = true }.Show(); break;
                case 20: foreach (Window w in Windows) if (w is GlassWindow) w.Close(); break;
                case 21: OsdWindow.ShowLevel(lo == 1 ? OsdKind.Brightness : OsdKind.Volume, hi / 100.0, false); break;
                case 22: AudioVolume.Instance.Level = lo / 100f; break;
                case 23: foreach (Window w in Windows) if (w is WelcomeWindow ww) ww.TestGo(lo); break;
                case 16: if (_settings != null) _settings.WindowState = lo == 1 ? WindowState.Maximized : WindowState.Normal; break;
            }
        }
        catch (Exception ex) { Log.Error("Test cmd lỗi", ex); }
    }

    /// <summary>Kiểm thử: xuất ảnh giao diện ra Paths.Root\shots bằng render ngoài màn hình.</summary>
    async void ExportShots(int what)
    {
        var dir = Path.Combine(Paths.Root, "shots"); Directory.CreateDirectory(dir);
        double scale = _docks.FirstOrDefault()?.Monitor.Scale ?? 1.25;
        var wall = new System.Windows.Media.LinearGradientBrush(new System.Windows.Media.GradientStopCollection
        {
            new(System.Windows.Media.Color.FromRgb(0x5B, 0x8D, 0xEF), 0), new(System.Windows.Media.Color.FromRgb(0xA7, 0x7B, 0xF0), 0.55), new(System.Windows.Media.Color.FromRgb(0xF6, 0xA8, 0x8C), 1)
        }, new Point(0, 0), new Point(1, 1));
        void Save(System.Windows.Media.Visual v, double w, double h, string name, bool bg = true)
        {
            int pw = (int)Math.Ceiling(w * scale), ph = (int)Math.Ceiling(h * scale);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, System.Windows.Media.PixelFormats.Pbgra32);
            if (bg) { var r = new System.Windows.Shapes.Rectangle { Width = w, Height = h, Fill = wall }; r.Measure(new Size(w, h)); r.Arrange(new Rect(0, 0, w, h)); rtb.Render(r); }
            rtb.Render(v);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using var fs = File.Create(Path.Combine(dir, name));
            enc.Save(fs);
        }
        FrameworkElement Card(FrameworkElement content)
        {
            var t = ThemeManager.Current;
            var b = new System.Windows.Controls.Border
            {
                Child = content, Background = t.PopupBackground, BorderBrush = t.PopupBorder, BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(14), Margin = new Thickness(18),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.25, Direction = 270 }
            };
            System.Windows.Documents.TextElement.SetForeground(b, t.PopupForeground);
            System.Windows.Documents.TextElement.SetFontFamily(b, t.Font);
            System.Windows.Documents.TextElement.SetFontSize(b, t.FontSize);
            return b;
        }
        try
        {
            if (what == 0)
            {
                var dock = _docks.First();
                Save(dock.View, dock.View.ActualWidth, dock.View.ActualHeight, "dock.png");
                var bar = _bars.First();
                var content = (System.Windows.Media.Visual)bar.Content;
                Save(content, bar.ActualWidth, bar.ActualHeight, "menubar.png");
            }
            else if (what == 1)
            {
                var dock = _docks.First();
                Save(dock.View, dock.View.ActualWidth, dock.View.ActualHeight, "dock-magnify.png");
            }
            else if (what >= 10 && _settings != null)
            {
                var sc = (FrameworkElement)_settings.Content;
                var bg = new System.Windows.Controls.Border { Background = ThemeManager.Current.WindowBackground, Width = sc.ActualWidth, Height = sc.ActualHeight };
                bg.Measure(new Size(sc.ActualWidth, sc.ActualHeight)); bg.Arrange(new Rect(0, 0, sc.ActualWidth, sc.ActualHeight));
                int pw = (int)Math.Ceiling(sc.ActualWidth * scale), ph = (int)Math.Ceiling(sc.ActualHeight * scale);
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(bg); rtb.Render(sc);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using var fs = File.Create(Path.Combine(dir, $"cai-dat-{what - 10}.png"));
                enc.Save(fs);
            }
            else
            {
                foreach (var (name, make) in new (string, Func<FrameworkElement>)[] { ("control-center.png", MenuBarPopups.ControlCenter), ("lich.png", MenuBarPopups.Calendar), ("am-thanh.png", MenuBarPopups.Volume) })
                {
                    var host = new System.Windows.Controls.Border { Child = Card(make()) };
                    var win = new Window { WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = System.Windows.Media.Brushes.Transparent, ShowInTaskbar = false, ShowActivated = false, Left = -4000, Top = -4000, SizeToContent = SizeToContent.WidthAndHeight, Content = host };
                    win.Show();
                    await Task.Delay(2000);
                    Save(host, host.ActualWidth, host.ActualHeight, name);
                    win.Close();
                }
            }
            Log.Info("Đã xuất ảnh vào " + dir);
        }
        catch (Exception ex) { Log.Error("Xuất ảnh lỗi", ex); }
    }

    // ------------------------------------------------------------------ tiện ích

    public void ShowSettings(string? page = null)
    {
        if (_settings == null || !_settings.IsLoaded)
        {
            _settings = new SettingsWindow();
            _settings.Closed += (_, _) => { _settings = null; if (Config.Current.Performance.TrimMemory) Dispatcher.BeginInvoke(async () => { await Task.Delay(2000); TrimMemory(); }); };
            _settings.Show();
        }
        if (page != null) _settings.Navigate(page);
        if (_settings.WindowState == WindowState.Minimized) _settings.WindowState = WindowState.Normal;
        _settings.Activate();
        WindowActions.Activate(new WindowInteropHelper(_settings).Handle);
    }

    public void FinishWelcome() => StartShell();

    public static void TrimMemory()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            Native.TrimWorkingSet();
        }
        catch { }
    }

    public void Quit()
    {
        if (_quitting) return;
        _quitting = true;
        Log.Info("Đang thoát MacDock");
        try
        {
            _maintenance?.Stop();
            WindowTracker.Instance.Stop();
            foreach (var b in _bars) b.Shutdown();
            foreach (var d in _docks) d.Shutdown();
            _bars.Clear(); _docks.Clear();
            Config.Save();
        }
        catch (Exception ex) { Log.Error("Lỗi khi thoát", ex); }
        EmergencyCleanup();
        if (_msgWindow != null) { Native.DeregisterShellHookWindow(_msgWindow.Handle); _msgWindow.Dispose(); }
        Shutdown();
    }

    /// <summary>Luôn khôi phục taskbar và gỡ khay hệ thống — gọi được từ mọi luồng, chỉ chạy một lần.</summary>
    public static void EmergencyCleanup()
    {
        if (Interlocked.Exchange(ref _cleanedUp, 1) == 1) return;
        try { MediaKeys.Stop(); } catch { }
        try { TrayService.Instance.Stop(); } catch { }
        // Khôi phục trên luồng riêng để không treo khi Explorer phản hồi chậm; nếu quá 4 giây thì watchdog sẽ làm nốt
        if (Application.Current?.Dispatcher.CheckAccess() == true) TaskbarController.PrepareRestoreOnUiThread();
        var th = new Thread(() => { try { TaskbarController.Restore(); } catch { } }) { IsBackground = true };
        th.Start();
        th.Join(4000);
    }
}
