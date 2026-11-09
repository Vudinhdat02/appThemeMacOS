using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MacDock;

/// <summary>Thanh Menu ở cạnh trên một màn hình.</summary>
public sealed class MenuBarWindow : Window
{
    public MonitorInfo Monitor { get; private set; }
    public IntPtr Hwnd { get; private set; }
    AppBar? _appBar;
    readonly Border _bg;
    readonly StackPanel _left, _right, _tray;
    readonly Dictionary<TrayIcon, FrameworkElement> _trayEls = new();
    bool _fullscreenHidden;
    DateTime _lastLayout;
    bool _inLayout;

    // menu đang mở
    readonly List<(FrameworkElement el, Func<ContextMenu?> open)> _topButtons = new();
    ContextMenu? _openMenu;
    FrameworkElement? _openOwner;
    DispatcherTimer? _menuTracker;
    int _menuGeneration;

    MenuBarSettings Cfg => Config.Current.MenuBar;
    ResolvedTheme T => ThemeManager.Current;

    public MenuBarWindow(MonitorInfo m)
    {
        Monitor = m;
        Title = "MacDock Menu Bar";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

        _left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch, Margin = new Thickness(8, 0, 0, 0) };
        _right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch, Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Right };
        _tray = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var leftClip = new Border { Child = _left, ClipToBounds = true };
        grid.Children.Add(leftClip);
        Grid.SetColumn(_right, 1);
        grid.Children.Add(_right);
        _bg = new Border { Child = grid };
        Content = _bg;

        DpiChanged += (_, _) => { if (!_inLayout) Dispatcher.BeginInvoke(() => LayoutWindow(), DispatcherPriority.Background); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Hwnd = new WindowInteropHelper(this).Handle;
        Native.AddExStyle(Hwnd, Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
        int pref = 1;
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
            else if (code == Native.ABN_FULLSCREENAPP) App.Instance.CheckFullscreen();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Init()
    {
        new WindowInteropHelper(this).EnsureHandle();
        ApplyTheme();
        LayoutWindow();
        Show();
        LayoutWindow();
        RebuildLeft();
        RebuildRight();
    }

    public void SetMonitor(MonitorInfo m) { Monitor = m; LayoutWindow(); }

    public void LayoutWindow()
    {
        if (Hwnd == IntPtr.Zero || _inLayout) return;
        _inLayout = true;
        try
        {
            var m = Monitors.FromHandle(Monitor.Handle) ?? Monitor;
            Monitor = m;
            int h = (int)Math.Ceiling(Cfg.Height * m.Scale);
            RECT rc;
            if (!_fullscreenHidden) rc = _appBar!.SetPosition(Native.ABE_TOP, m.Bounds, h);
            else rc = new RECT(m.Bounds.Left, m.Bounds.Top, m.Bounds.Right, m.Bounds.Top + h);
            Native.SetWindowPos(Hwnd, IntPtr.Zero, rc.Left, rc.Top, rc.Width, h, Native.SWP_NOACTIVATE | Native.SWP_NOZORDER);
            _lastLayout = DateTime.Now;
        }
        catch (Exception ex) { Log.Error("MenuBar layout lỗi", ex); }
        finally { _inLayout = false; }
    }

    public void ApplyTheme()
    {
        var t = T;
        FontFamily = t.Font;
        FontSize = t.FontSize;
        Foreground = t.MenuBarForeground;
        _bg.Background = t.MenuBarBackground;
        _bg.BorderBrush = t.Separator;
        _bg.BorderThickness = new Thickness(0, 0, 0, t.BorderThickness > 0 ? 0.5 : 0);
        if (Hwnd != IntPtr.Zero)
        {
            var c = t.MenuBarColor;
            uint tint = (uint)(0x18 << 24) | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;
            switch (t.MenuBarMaterial)
            {
                case "acrylic": Native.SetAccent(Hwnd, 4, tint); break;
                case "blur": Native.SetAccent(Hwnd, 3, 0); break;
                default: Native.SetAccent(Hwnd, 0, 0); break;
            }
        }
    }

    public void SetFullscreenHidden(bool hidden)
    {
        if (_fullscreenHidden == hidden) return;
        _fullscreenHidden = hidden;
        if (hidden) { CloseMenus(); Hide(); }
        else { Show(); LayoutWindow(); BringTopmost(); }
    }

    public void BringTopmost()
    {
        if (Hwnd != IntPtr.Zero && !_fullscreenHidden)
            Native.SetWindowPos(Hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    public void Shutdown()
    {
        CloseMenus();
        try { _appBar?.Unregister(); } catch { }
        Close();
    }

    // ------------------------------------------------------------------ phần tử

    FrameworkElement Btn(object content, Action<FrameworkElement>? onClick, double padH = 7, string? tooltip = null)
    {
        var b = new Border
        {
            Child = content as UIElement ?? new TextBlock { Text = content.ToString() },
            Padding = new Thickness(padH, 0, padH, 0),
            Margin = new Thickness(1, 3, 1, 3),
            CornerRadius = new CornerRadius(5),
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        if (content is FrameworkElement fe) { fe.VerticalAlignment = VerticalAlignment.Center; }
        if (tooltip != null) b.ToolTip = tooltip;
        b.MouseEnter += (_, _) => { if (_openOwner != b) b.Background = T.MenuHighlight; };
        b.MouseLeave += (_, _) => { if (_openOwner != b) b.Background = Brushes.Transparent; };
        if (onClick != null)
            b.MouseLeftButtonUp += (_, e) => { e.Handled = true; onClick(b); };
        return b;
    }

    TextBlock Text(string s, bool bold = false) => new()
    {
        Text = s,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = T.MenuBarForeground,
    };

    // ------------------------------------------------------------------ bên trái

    public void RebuildLeft()
    {
        if (!IsLoaded && Hwnd == IntPtr.Zero) return;
        Diag.MenuRebuilds++;
        _left.Children.Clear();
        _topButtons.Clear();
        int gen = ++_menuGeneration;

        var logo = Btn(Glyphs.Icon(Glyphs.Logo, T.MenuBarForeground, 15), null, 9);
        AddTop(logo, () => MenuBarMenus.SystemMenu());
        _left.Children.Add(logo);

        var app = WindowTracker.Instance.ForegroundApp;
        IntPtr hwnd = WindowTracker.Instance.ForegroundAppHwnd;
        string name = app != null ? AppIdentity.DisplayName(app.Key, app.ExePath, app.Aumid) : "File Explorer";
        if (name.Length > 36) name = name.Substring(0, 34) + "…";
        var appBtn = Btn(Text(name, Cfg.BoldAppName), null, 8);
        AddTop(appBtn, () => MenuBarMenus.AppMenu(app, hwnd, name));
        _left.Children.Add(appBtn);

        if (!Cfg.ShowAppMenus) return;

        List<TopMenu>? menus = null;
        bool own = app?.Pid == Environment.ProcessId;
        if (!own)
        {
            try { menus = AppMenuProvider.GetWin32Menus(hwnd); } catch (Exception ex) { Log.Debug("Win32 menu: " + ex.Message); }
        }
        if (menus != null) { AddMenus(menus); return; }

        AddMenus(own ? MenuBarMenus.OwnMenus() : AppMenuProvider.Generic(hwnd, app));
        if (!own && Cfg.UseUIAutomation && hwnd != IntPtr.Zero)
        {
            _ = LoadUia(hwnd, app, gen);
        }
    }

    async Task LoadUia(IntPtr hwnd, AppId? app, int gen)
    {
        try
        {
            var menus = await AppMenuProvider.GetUiaMenus(hwnd);
            if (menus == null || gen != _menuGeneration) return;
            // giữ logo + tên app, thay phần menu chung bằng menu thật
            while (_left.Children.Count > 2) _left.Children.RemoveAt(2);
            _topButtons.RemoveRange(2, _topButtons.Count - 2);
            AddMenus(menus);
        }
        catch (Exception ex) { Log.Debug("UIA menus: " + ex.Message); }
    }

    void AddMenus(List<TopMenu> menus)
    {
        foreach (var m in menus.Take(14))
        {
            var tm = m;
            var b = Btn(Text(tm.Title), null, 8);
            if (tm.Build != null) AddTop(b, () => tm.Build());
            else b.MouseLeftButtonUp += (_, e) => { e.Handled = true; CloseMenus(); tm.Invoke?.Invoke(); };
            _left.Children.Add(b);
        }
    }

    void AddTop(FrameworkElement el, Func<ContextMenu?> open)
    {
        _topButtons.Add((el, open));
        el.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (_openOwner == el) { CloseMenus(); return; }
            OpenMenu(el, open);
        };
    }

    void OpenMenu(FrameworkElement owner, Func<ContextMenu?> open)
    {
        CloseMenus();
        ContextMenu? cm;
        try { cm = open(); } catch (Exception ex) { Log.Error("Mở menu lỗi", ex); return; }
        if (cm == null) return;
        _openMenu = cm;
        _openOwner = owner;
        if (owner is Border b) b.Background = T.MenuHighlight;
        cm.PlacementTarget = owner;
        cm.Placement = PlacementMode.Bottom;
        cm.VerticalOffset = 3;
        cm.Closed += (_, _) =>
        {
            if (_openMenu != cm) return;
            if (_openOwner is Border ob) ob.Background = Brushes.Transparent;
            _openMenu = null; _openOwner = null;
            _menuTracker?.Stop();
        };
        cm.IsOpen = true;
        // khi một menu đang mở, rê sang mục khác thì chuyển menu (giống macOS)
        _menuTracker ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _menuTracker.Tick -= TrackMenus;
        _menuTracker.Tick += TrackMenus;
        _menuTracker.Start();
    }

    void TrackMenus(object? s, EventArgs e)
    {
        if (_openMenu == null) { _menuTracker?.Stop(); return; }
        if (!Native.GetCursorPos(out var pt)) return;
        foreach (var (el, open) in _topButtons)
        {
            if (el == _openOwner || !el.IsVisible) continue;
            var p = el.PointFromScreen(new Point(pt.X, pt.Y));
            if (p.X >= 0 && p.Y >= 0 && p.X < el.ActualWidth && p.Y < el.ActualHeight)
            {
                OpenMenu(el, open);
                return;
            }
        }
    }

    public void CloseMenus()
    {
        if (_openMenu != null) { var m = _openMenu; _openMenu = null; m.IsOpen = false; }
        if (_openOwner is Border ob) ob.Background = Brushes.Transparent;
        _openOwner = null;
        _menuTracker?.Stop();
    }

    // ------------------------------------------------------------------ bên phải

    TextBlock? _clockText;

    public void RebuildRight()
    {
        Diag.StatusChanges++;
        _right.Children.Clear();
        var st = StatusService.Instance;
        var fg = T.MenuBarForeground;

        if (Cfg.ShowTray && TrayService.Instance.Running)
        {
            _right.Children.Add(_tray);
            RebuildTray();
        }

        if (Cfg.ShowInputLang && st.InputLang.Length > 0)
        {
            var t = Text(st.InputLang);
            t.FontSize = Math.Max(10, T.FontSize - 1.5);
            t.FontWeight = FontWeights.SemiBold;
            var box = new Border { Child = t, BorderBrush = fg, BorderThickness = new Thickness(1.1), CornerRadius = new CornerRadius(3), Padding = new Thickness(3, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.9 };
            var b = Btn(box, null, 6, "Bộ gõ / ngôn ngữ");
            AddTop(b, MenuBarMenus.LanguageMenu);
            _right.Children.Add(b);
        }

        if (Cfg.ShowBattery && st.BatteryPercent >= 0)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            if (Cfg.ShowBatteryPercent) { var tt = Text(st.BatteryPercent + "%"); tt.Margin = new Thickness(0, 0, 5, 0); tt.FontSize = T.FontSize - 0.5; sp.Children.Add(tt); }
            var bi = Glyphs.BatteryIcon(fg, st.BatteryPercent, st.Charging, 11.5);
            bi.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(bi);
            _right.Children.Add(Btn(sp, el => ShowPopup(el, MenuBarPopups.Battery()), 7, null));
        }

        if (Cfg.ShowWifi)
        {
            FrameworkElement icon = st.Net.Kind == NetKind.Ethernet ? Glyphs.Icon(Glyphs.Ethernet, fg, 15)
                : Glyphs.WifiIcon(fg, st.Net.Kind == NetKind.Wifi ? st.Net.SignalPercent : 0, 16);
            if (st.Net.Kind == NetKind.None) icon.Opacity = 0.45;
            _right.Children.Add(Btn(icon, el => ShowPopup(el, MenuBarPopups.Network()), 7));
        }

        if (Cfg.ShowVolume && st.AudioAvailable)
        {
            var b = Btn(Glyphs.VolumeIcon(fg, st.Volume, st.Muted, 16), el => ShowPopup(el, MenuBarPopups.Volume()), 7);
            b.MouseWheel += (_, e) =>
            {
                AudioVolume.Instance.Level = Math.Clamp(AudioVolume.Instance.Level + (e.Delta > 0 ? 0.04f : -0.04f), 0f, 1f);
                if (AudioVolume.Instance.Muted && e.Delta > 0) AudioVolume.Instance.Muted = false;
                e.Handled = true;
            };
            _right.Children.Add(b);
        }

        if (Cfg.ShowControlCenter)
            _right.Children.Add(Btn(Glyphs.ControlCenterIcon(fg, 15), el => ShowPopup(el, MenuBarPopups.ControlCenter()), 7, "Trung tâm điều khiển"));

        if (Cfg.ShowNotifications)
            _right.Children.Add(Btn(Glyphs.Icon(Glyphs.Bell, fg, 15), _ => { CloseMenus(); Native.SendKeys(Native.VK_LWIN, 0x4E); }, 7, "Thông báo (Win+N)"));

        _clockText = Text(st.ClockText);
        _clockText.Margin = new Thickness(2, 0, 2, 0);
        _right.Children.Add(Btn(_clockText, el => ShowPopup(el, MenuBarPopups.Calendar()), 7));
    }

    public void UpdateClock()
    {
        if (_clockText != null) _clockText.Text = StatusService.Instance.ClockText;
    }

    public void RebuildTray()
    {
        Diag.TrayChanges++;
        // Nếu danh sách icon không đổi, chỉ cập nhật ảnh/tooltip (icon động) — rẻ hơn nhiều
        var hiddenSet = new HashSet<string>(Cfg.HiddenTrayIcons, StringComparer.OrdinalIgnoreCase);
        var visible = TrayService.Instance.Icons.OrderBy(i => i.Order).Where(i => !i.Hidden && i.Icon != null && !hiddenSet.Contains(i.PersistKey)).ToList();
        if (Cfg.ShowTray && visible.Count == _trayEls.Count && visible.All(_trayEls.ContainsKey))
        {
            foreach (var ic in visible)
            {
                var el = (Border)_trayEls[ic];
                if (el.Child is Image img && !ReferenceEquals(img.Source, ic.Icon)) img.Source = ic.Icon;
                object? tip = string.IsNullOrWhiteSpace(ic.Tip) ? null : ic.Tip;
                if (!Equals(el.ToolTip, tip)) el.ToolTip = tip;
            }
            return;
        }
        _tray.Children.Clear();
        _trayEls.Clear();
        if (!Cfg.ShowTray) return;
        var hidden = new HashSet<string>(Cfg.HiddenTrayIcons, StringComparer.OrdinalIgnoreCase);
        foreach (var icon in TrayService.Instance.Icons.OrderBy(i => i.Order))
        {
            if (icon.Hidden || icon.Icon == null || hidden.Contains(icon.PersistKey)) continue;
            var ic = icon;
            var img = new Image { Source = icon.Icon, Width = 16, Height = 16, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            var b = new Border
            {
                Child = img, Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(0, 3, 0, 3),
                CornerRadius = new CornerRadius(5), Background = Brushes.Transparent,
                ToolTip = string.IsNullOrWhiteSpace(icon.Tip) ? null : icon.Tip
            };
            b.MouseEnter += (_, _) => { b.Background = T.MenuHighlight; SendTray(ic, TrayService.Click.Hover); };
            b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { SendTray(ic, TrayService.Click.Double); e.Handled = true; } };
            b.MouseLeftButtonUp += (_, e) => { e.Handled = true; CloseMenus(); SendTray(ic, TrayService.Click.Left); };
            b.MouseRightButtonUp += (_, e) => { e.Handled = true; CloseMenus(); SendTray(ic, TrayService.Click.Right); };
            b.MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { SendTray(ic, TrayService.Click.Middle); e.Handled = true; } };
            _tray.Children.Add(b);
            _trayEls[icon] = b;
        }
    }

    void SendTray(TrayIcon icon, TrayService.Click click)
    {
        Native.GetCursorPos(out var pt);
        // neo menu của ứng dụng ngay dưới icon (không bị Thanh Menu che)
        if (click != TrayService.Click.Hover && TrayIconRect(icon) is { } r) { pt.X = (r.Left + r.Right) / 2; pt.Y = r.Bottom + 2; }
        TrayService.Instance.SendMouse(icon, click, pt.X, pt.Y);
    }

    public RECT? TrayIconRect(TrayIcon icon)
    {
        if (!_trayEls.TryGetValue(icon, out var el) || !el.IsVisible) return null;
        try
        {
            var p0 = el.PointToScreen(new Point(0, 0));
            var p1 = el.PointToScreen(new Point(el.ActualWidth, el.ActualHeight));
            return new RECT((int)p0.X, (int)p0.Y, (int)p1.X, (int)p1.Y);
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------ kiểm thử (--test)

    public void TestOpenTop(int i) { if (i >= 0 && i < _topButtons.Count) OpenMenu(_topButtons[i].el, _topButtons[i].open); }
    public void TestPopup(int k)
    {
        var owner = _right.Children.Count > 0 ? (FrameworkElement)_right.Children[^1] : this;
        FrameworkElement content = k switch { 0 => MenuBarPopups.Battery(), 1 => MenuBarPopups.Network(), 2 => MenuBarPopups.Volume(), 3 => MenuBarPopups.ControlCenter(), _ => MenuBarPopups.Calendar() };
        ShowPopup(owner, content);
    }
    public void TestClosePopups() { CloseMenus(); if (_popup != null) _popup.IsOpen = false; }
    public string TestTrayClick(int i, bool right)
    {
        var icons = _trayEls.Keys.OrderBy(x => x.Order).ToList();
        if (i < 0 || i >= icons.Count) return "none";
        var r = TrayIconRect(icons[i]);
        int x = r != null ? (r.Value.Left + r.Value.Right) / 2 : 0, y = r != null ? r.Value.Bottom + 2 : 0;
        TrayService.Instance.SendMouse(icons[i], right ? TrayService.Click.Right : TrayService.Click.Left, x, y);
        return icons[i].ToString();
    }

    // ------------------------------------------------------------------ popup

    Popup? _popup;

    void ShowPopup(FrameworkElement owner, FrameworkElement content)
    {
        CloseMenus();
        if (_popup != null) { _popup.IsOpen = false; _popup = null; }
        var t = T;
        var border = new Border
        {
            Child = content,
            Background = t.PopupBackground,
            BorderBrush = t.PopupBorder,
            BorderThickness = new Thickness(0.8),
            CornerRadius = new CornerRadius(Math.Max(10, t.PopupCornerRadius + 3)),
            Padding = new Thickness(14),
            Margin = new Thickness(12),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.25, Direction = 270 },
        };
        TextElement.SetForeground(border, t.PopupForeground);
        TextElement.SetFontFamily(border, t.Font);
        TextElement.SetFontSize(border, t.FontSize);
        var popup = new Popup
        {
            Child = border,
            PlacementTarget = owner,
            Placement = PlacementMode.Bottom,
            AllowsTransparency = true,
            StaysOpen = false,
            PopupAnimation = App.EffectsOff ? PopupAnimation.None : PopupAnimation.Fade,
            HorizontalOffset = -12,
            VerticalOffset = -8,
        };
        if (owner is Border ob) { ob.Background = T.MenuHighlight; popup.Closed += (_, _) => ob.Background = Brushes.Transparent; }
        popup.Opened += (_, _) => Log.Debug("Popup mở");
        popup.Closed += (_, _) => { Log.Debug("Popup đóng"); if (_popup == popup) _popup = null; };
        _popup = popup;
        popup.IsOpen = true;
        MenuBarPopups.CloseRequested = () => popup.IsOpen = false;
    }
}
