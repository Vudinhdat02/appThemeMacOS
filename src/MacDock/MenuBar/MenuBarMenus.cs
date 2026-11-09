using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static MacDock.DockModel;

namespace MacDock;

/// <summary>Các menu của Menu Bar: menu hệ thống (logo), menu ứng dụng, bộ gõ.</summary>
public static class MenuBarMenus
{
    public static ContextMenu SystemMenu()
    {
        var cm = new ContextMenu();
        cm.Items.Add(Mi(L.T("Giới thiệu máy này"), () => AboutPcWindow.ShowSingle()));
        cm.Items.Add(new Separator());
        cm.Items.Add(Mi(L.T("Cài đặt Windows…"), () => Launcher.Open("ms-settings:")));
        cm.Items.Add(Mi(L.T("Cài đặt MacDock…"), () => App.Instance.ShowSettings()));
        cm.Items.Add(Mi("Task Manager", () => Launcher.Open("taskmgr.exe")));
        cm.Items.Add(new Separator());
        cm.Items.Add(Mi(L.T("Ngủ"), PowerActions.Sleep));
        cm.Items.Add(Mi(L.T("Khởi động lại…"), PowerActions.Restart));
        cm.Items.Add(Mi(L.T("Tắt máy…"), PowerActions.Shutdown));
        cm.Items.Add(new Separator());
        var lockItem = Mi(L.T("Khoá màn hình"), PowerActions.Lock); lockItem.InputGestureText = "Win+L";
        cm.Items.Add(lockItem);
        cm.Items.Add(Mi(L.F("Đăng xuất {0}…", Environment.UserName), PowerActions.LogOff));
        return cm;
    }

    public static ContextMenu AppMenu(AppId? app, IntPtr hwnd, string name)
    {
        var cm = new ContextMenu();
        bool own = app?.Pid == Environment.ProcessId;
        cm.Items.Add(Mi(L.F("Giới thiệu {0}", name), () => ShowAppInfo(app, name)));
        if (own)
        {
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi(L.T("Cài đặt…"), () => App.Instance.ShowSettings()));
        }
        cm.Items.Add(new Separator());
        if (app != null)
        {
            cm.Items.Add(Mi(L.F("Ẩn {0}", name), () => { foreach (var w in WindowTracker.Instance.WindowsOf(app.Key)) WindowActions.Minimize(w.Hwnd); }));
            cm.Items.Add(Mi(L.T("Ẩn ứng dụng khác"), () =>
            {
                foreach (var w in WindowTracker.Instance.Windows.Where(w => w.Id.Key != app.Key).ToList()) WindowActions.Minimize(w.Hwnd);
            }));
            cm.Items.Add(Mi(L.T("Hiện tất cả"), () =>
            {
                foreach (var w in WindowTracker.Instance.Windows.Where(w => w.Minimized).ToList()) Native.ShowWindowAsync(w.Hwnd, Native.SW_SHOWNOACTIVATE);
            }));
            cm.Items.Add(new Separator());
            if (app.ExePath != null && !own)
                cm.Items.Add(Mi(L.T("Hiện trong Explorer"), () => Launcher.ShowInExplorer(app.ExePath)));
            if (own)
                cm.Items.Add(Mi(L.T("Thoát MacDock"), () => App.Instance.Quit()));
            else
            {
                var quit = Mi(L.F("Thoát {0}", name), () => { foreach (var w in WindowTracker.Instance.WindowsOf(app.Key)) WindowActions.Close(w.Hwnd); });
                quit.InputGestureText = "Alt+F4";
                cm.Items.Add(quit);
                cm.Items.Add(Mi(L.T("Buộc thoát…"), () =>
                {
                    if (ConfirmDialog.Ask(L.F("Buộc thoát {0}? Dữ liệu chưa lưu sẽ bị mất.", name), "", L.T("Buộc thoát"), true))
                        WindowActions.KillProcess(app.Pid);
                }));
            }
        }
        return cm;
    }

    static void ShowAppInfo(AppId? app, string name)
    {
        string text = name;
        try
        {
            if (app?.ExePath != null && File.Exists(app.ExePath))
            {
                var vi = FileVersionInfo.GetVersionInfo(app.ExePath);
                text = L.F("{0}\n\nPhiên bản: {1}\nNhà phát hành: {2}\nĐường dẫn: {3}", name, vi.ProductVersion ?? vi.FileVersion, vi.CompanyName, app.ExePath);
            }
            if (app?.Aumid != null) text += $"\nAUMID: {app.Aumid}";
            if (app?.Pid == Environment.ProcessId) text = L.F("MacDock {0}\nDock, Thanh Menu và giao diện cho Windows 11.", App.VersionString);
        }
        catch { }
        MessageBox.Show(text, L.T("Giới thiệu"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static List<TopMenu> OwnMenus() => new()
    {
        new() { Title = L.T("Tệp"), Build = () => new ContextMenu { Items = { Mi(L.T("Nhập theme…"), () => App.Instance.ShowSettings("theme")), Mi(L.T("Mở thư mục dữ liệu"), () => Launcher.Open(Paths.Root)) } } },
        new() { Title = L.T("Cửa sổ"), Build = () => AppMenuProvider.WindowMenu(WindowTracker.Instance.ForegroundAppHwnd, WindowTracker.Instance.ForegroundApp) },
        new() { Title = L.T("Trợ giúp"), Build = () => new ContextMenu { Items = {
            Mi(L.T("Khôi phục taskbar Windows"), () => { TaskbarController.Restore(); Config.Current.General.HideTaskbar = false; Config.Touch("general"); }),
            Mi(L.T("Xem nhật ký (log)"), () => Launcher.Open(Paths.LogFile)) } } },
    };

    public static ContextMenu LanguageMenu()
    {
        var cm = new ContextMenu();
        var cur = StatusService.Instance.InputHkl;
        foreach (var hkl in StatusService.Layouts())
        {
            var h = hkl;
            var mi = Mi($"{StatusService.LangCode(h)}  —  {StatusService.LangName(h)}", () => StatusService.Instance.SwitchLayout(h));
            mi.IsChecked = (h.ToInt64() & 0xFFFFFFFF) == (cur.ToInt64() & 0xFFFFFFFF);
            cm.Items.Add(mi);
        }
        cm.Items.Add(new Separator());
        var sw = Mi(L.T("Chuyển bộ gõ tiếp theo"), () => Native.SendKeys(Native.VK_LWIN, 0x20)); sw.InputGestureText = "Win+Space";
        cm.Items.Add(sw);
        cm.Items.Add(Mi(L.T("Cài đặt ngôn ngữ…"), () => Launcher.Open("ms-settings:regionlanguage")));
        return cm;
    }
}

/// <summary>Các bảng bật lên (popup) bên phải Menu Bar.</summary>
public static class MenuBarPopups
{
    public static Action? CloseRequested;
    static ResolvedTheme T => ThemeManager.Current;

    static TextBlock Title(string s) => new() { Text = s, FontWeight = FontWeights.SemiBold, FontSize = T.FontSize + 0.5, Margin = new Thickness(0, 0, 0, 8) };
    static TextBlock Line(string s, double opacity = 1) => new() { Text = s, Opacity = opacity, Margin = new Thickness(0, 1, 0, 1), TextWrapping = TextWrapping.Wrap };

    static FrameworkElement Link(string text, Action a)
    {
        var tb = new TextBlock { Text = text, Padding = new Thickness(8, 5, 8, 5) };
        var b = new Border { Child = tb, CornerRadius = new CornerRadius(5), Background = Brushes.Transparent, Margin = new Thickness(-8, 1, -8, 1), Cursor = System.Windows.Input.Cursors.Hand };
        b.MouseEnter += (_, _) => { b.Background = T.Selection; tb.Foreground = T.SelectionForeground; };
        b.MouseLeave += (_, _) => { b.Background = Brushes.Transparent; tb.ClearValue(TextBlock.ForegroundProperty); };
        b.MouseLeftButtonUp += (_, _) => { CloseRequested?.Invoke(); a(); };
        return b;
    }

    static FrameworkElement Sep() => new Border { Height = 1, Background = T.Separator, Margin = new Thickness(0, 7, 0, 7) };

    static StackPanel Panel(double width = 260) => new() { Width = width };

    public static FrameworkElement Battery()
    {
        var st = StatusService.Instance;
        var p = Panel();
        p.Children.Add(Title("Pin"));
        p.Children.Add(Line($"{st.BatteryPercent}%  {(st.Charging ? L.T("— đang sạc") : st.OnBattery ? L.T("— đang dùng pin") : L.T("— đã cắm sạc"))}"));
        if (Native.GetSystemPowerStatus(out var s) && s.BatteryLifeTime > 0)
            p.Children.Add(Line(L.F("Còn khoảng {0} giờ {1} phút", (int)TimeSpan.FromSeconds(s.BatteryLifeTime).TotalHours, TimeSpan.FromSeconds(s.BatteryLifeTime).Minutes), 0.7));
        p.Children.Add(Sep());
        p.Children.Add(Link(L.T("Cài đặt Pin…"), () => Launcher.Open("ms-settings:batterysaver")));
        p.Children.Add(Link(L.T("Nguồn & Ngủ…"), () => Launcher.Open("ms-settings:powersleep")));
        return p;
    }

    public static FrameworkElement Network()
    {
        var st = StatusService.Instance;
        var p = Panel(300);
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var title = new TextBlock { Text = "Wi-Fi", FontWeight = FontWeights.SemiBold, FontSize = T.FontSize + 1, VerticalAlignment = VerticalAlignment.Center };
        var toggle = new CheckBox { IsChecked = st.Net.Kind == NetKind.Wifi, VerticalAlignment = VerticalAlignment.Center };
        toggle.SetResourceReference(FrameworkElement.StyleProperty, "Md.Toggle");
        DockPanel.SetDock(toggle, Dock.Right);
        head.Children.Add(toggle); head.Children.Add(title);
        p.Children.Add(head);
        var status = new TextBlock { Opacity = 0.65, FontSize = T.FontSize - 1, Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap };
        p.Children.Add(status);
        var list = new StackPanel();
        var scroll = new ScrollViewer { Content = list, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        p.Children.Add(scroll);
        p.Children.Add(Sep());
        p.Children.Add(Link(L.T("Cài đặt Mạng & Internet…"), () => Launcher.Open("ms-settings:network")));

        bool busy = false;
        async void Load(bool scan)
        {
            status.Text = scan ? L.T("Đang tìm mạng…") : "";
            var nets = await Task.Run(() => Wlan.Networks(scan));
            list.Children.Clear();
            if (nets.Count == 0) { status.Text = L.T("Không tìm thấy mạng Wi-Fi nào (Wi-Fi có thể đang tắt)."); return; }
            status.Text = "";
            var known = nets.Where(n => n.Connected || n.Profile != null).ToList();
            var others = nets.Except(known).ToList();
            void Section(string name, List<WifiNetwork> items)
            {
                if (items.Count == 0) return;
                list.Children.Add(new TextBlock { Text = name, FontSize = T.FontSize - 1.5, Opacity = 0.55, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) });
                foreach (var n in items) list.Children.Add(NetRow(n));
            }
            Section(L.T("Mạng đã biết"), known);
            Section(L.T("Mạng khác"), others);
        }

        FrameworkElement NetRow(WifiNetwork n)
        {
            var row = new DockPanel { Margin = new Thickness(0) };
            var icon = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = n.Connected ? T.Accent : new SolidColorBrush(Color.FromArgb(0x30, 0x80, 0x80, 0x88)), Margin = new Thickness(0, 0, 10, 0) };
            icon.Child = Glyphs.WifiIcon(n.Connected ? Brushes.White : T.PopupForeground, n.Signal, 15);
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            if (n.Secured)
            {
                var lk = Glyphs.Icon(Glyphs.Lock, T.PopupForeground, 12, true, 1.3);
                lk.Opacity = 0.55; lk.Margin = new Thickness(6, 0, 0, 0);
                DockPanel.SetDock(lk, Dock.Right);
                row.Children.Add(lk);
            }
            var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            txt.Children.Add(new TextBlock { Text = n.Ssid, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = n.Connected ? FontWeights.SemiBold : FontWeights.Normal });
            if (n.Connected) txt.Children.Add(new TextBlock { Text = L.T("Đã kết nối — bấm để ngắt"), FontSize = T.FontSize - 2, Opacity = 0.6 });
            row.Children.Add(txt);
            var b = new Border { Child = row, Padding = new Thickness(6, 5, 6, 5), Margin = new Thickness(-6, 1, -6, 1), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
            b.MouseEnter += (_, _) => b.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x88));
            b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonUp += async (_, _) =>
            {
                if (busy) return;
                if (n.Connected) { Wlan.Disconnect(n); status.Text = L.T("Đã ngắt kết nối ") + n.Ssid; await Task.Delay(1200); Load(false); return; }
                string? pass = null;
                if (n.Secured && n.Profile == null)
                {
                    CloseRequested?.Invoke();
                    pass = WifiPasswordWindow.Ask(n.Ssid);
                    if (pass == null) return;
                }
                busy = true;
                status.Text = L.T("Đang kết nối ") + n.Ssid + "…";
                var err = await Task.Run(() => Wlan.Connect(n, pass));
                if (err != null) { status.Text = err; busy = false; return; }
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(800);
                    var now = await Task.Run(() => Wlan.Query());
                    if (now.Kind == NetKind.Wifi && now.Name == n.Ssid) { status.Text = L.T("Đã kết nối ") + n.Ssid; StatusService.Instance.RefreshAll(); break; }
                }
                busy = false;
                Load(false);
            };
            return b;
        }

        toggle.Checked += async (_, _) => { await StatusService.SetRadio(Windows.Devices.Radios.RadioKind.WiFi, true); await Task.Delay(1500); Load(true); };
        toggle.Unchecked += async (_, _) => { await StatusService.SetRadio(Windows.Devices.Radios.RadioKind.WiFi, false); list.Children.Clear(); status.Text = L.T("Wi-Fi đã tắt"); };
        _ = StatusService.GetRadios().ContinueWith(t => App.Instance.Dispatcher.BeginInvoke(() =>
        {
            if (t.Result.wifi is bool on) toggle.IsChecked = on;
        }));
        Load(false);
        Task.Delay(300).ContinueWith(_ => App.Instance.Dispatcher.BeginInvoke(() => Load(true)));
        return p;
    }
    static Slider MakeSlider(double value, Action<double> changed)
    {
        var s = new Slider { Minimum = 0, Maximum = 100, Value = value, Width = 232, IsMoveToPointEnabled = true, Margin = new Thickness(0, 4, 0, 4) };
        s.ValueChanged += (_, e) => changed(e.NewValue);
        return s;
    }

    public static FrameworkElement Volume()
    {
        var p = Panel();
        p.Children.Add(Title(L.T("Âm thanh")));
        StatusService.Instance.RefreshAudio();
        var row = new DockPanel();
        var icon = new Border { Child = Glyphs.VolumeIcon(T.PopupForeground, StatusService.Instance.Volume, StatusService.Instance.Muted, 18), Margin = new Thickness(0, 0, 8, 0), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = L.T("Tắt/bật tiếng") };
        icon.MouseLeftButtonUp += (_, _) =>
        {
            AudioVolume.Instance.Muted = !AudioVolume.Instance.Muted;
            icon.Child = Glyphs.VolumeIcon(T.PopupForeground, AudioVolume.Instance.Level, AudioVolume.Instance.Muted, 18);
        };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        var label = new TextBlock { Text = $"{Math.Round(StatusService.Instance.Volume * 100)}%", Width = 36, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(label, Dock.Right);
        row.Children.Add(label);
        var slider = MakeSlider(StatusService.Instance.Volume * 100, v =>
        {
            AudioVolume.Instance.Level = (float)(v / 100);
            if (AudioVolume.Instance.Muted && v > 0) AudioVolume.Instance.Muted = false;
            label.Text = $"{Math.Round(v)}%";
        });
        slider.Width = 170;
        row.Children.Add(slider);
        p.Children.Add(row);
        p.Children.Add(Sep());
        p.Children.Add(Link(L.T("Cài đặt âm thanh…"), () => Launcher.Open("ms-settings:sound")));
        p.Children.Add(Link(L.T("Bộ trộn âm lượng…"), () => Launcher.Open("ms-settings:apps-volume")));
        return p;
    }

    public static FrameworkElement Calendar()
    {
        var p = Panel(250);
        var now = DateTime.Now;
        var vi = L.Culture;
        p.Children.Add(new TextBlock { Text = now.ToString("dddd", vi), FontSize = T.FontSize + 1, Opacity = 0.7 });
        p.Children.Add(new TextBlock { Text = now.ToString("d MMMM, yyyy", vi), FontSize = T.FontSize + 5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        var cal = new System.Windows.Controls.Calendar { SelectedDate = now.Date, DisplayDate = now.Date, Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.Name), FirstDayOfWeek = DayOfWeek.Monday, HorizontalAlignment = HorizontalAlignment.Center };
        p.Children.Add(cal);
        p.Children.Add(Sep());
        p.Children.Add(Link(L.T("Mở Thông báo & Lịch (Win+N)"), () => Native.SendKeys(Native.VK_LWIN, 0x4E)));
        p.Children.Add(Link(L.T("Cài đặt ngày giờ…"), () => Launcher.Open("ms-settings:dateandtime")));
        return p;
    }

    // ---------------- Trung tâm điều khiển ----------------

    sealed class Tile
    {
        public Border Root = null!;
        public Border Circle = null!;
        public TextBlock State = null!;
        public bool On;
    }

    static Tile MakeTile(Geometry glyph, string title, bool stroke, Action<Tile> click)
    {
        var t = new Tile();
        t.Circle = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Child = Glyphs.Icon(glyph, Brushes.White, 15, stroke, 1.6), Margin = new Thickness(0, 0, 9, 0) };
        t.State = new TextBlock { Text = "…", Opacity = 0.65, FontSize = T.FontSize - 1.5 };
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        texts.Children.Add(t.State);
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(t.Circle); sp.Children.Add(texts);
        t.Root = new Border
        {
            Child = sp, Padding = new Thickness(10, 9, 10, 9), Margin = new Thickness(3), CornerRadius = new CornerRadius(12),
            Background = T.CardBackground, BorderBrush = T.Separator, BorderThickness = new Thickness(0.5), Cursor = System.Windows.Input.Cursors.Hand
        };
        t.Root.MouseLeftButtonUp += (_, _) => click(t);
        SetTile(t, false, "…");
        return t;
    }

    static void SetTile(Tile t, bool on, string state)
    {
        t.On = on;
        t.Circle.Background = on ? T.Accent : new SolidColorBrush(Color.FromArgb(0x80, 0x80, 0x80, 0x88));
        t.State.Text = state;
    }

    public static FrameworkElement ControlCenter()
    {
        var root = Panel(300);
        var grid = new UniformGrid { Columns = 2 };

        Tile wifi = null!, bt = null!;
        wifi = MakeTile(Glyphs.WifiFull, "Wi-Fi", true, async t =>
        {
            bool target = !t.On;
            SetTile(t, target, target ? L.T("Đang bật…") : L.T("Đang tắt…"));
            if (!await StatusService.SetRadio(Windows.Devices.Radios.RadioKind.WiFi, target)) Launcher.Open("ms-settings:network-wifi");
            await Task.Delay(800);
            await RefreshRadios();
        });
        bt = MakeTile(Glyphs.Bluetooth, "Bluetooth", true, async t =>
        {
            bool target = !t.On;
            SetTile(t, target, target ? L.T("Đang bật…") : L.T("Đang tắt…"));
            if (!await StatusService.SetRadio(Windows.Devices.Radios.RadioKind.Bluetooth, target)) Launcher.Open("ms-settings:bluetooth");
            await Task.Delay(800);
            await RefreshRadios();
        });
        bool dark = ThemeManager.SystemIsDark();
        var darkTile = MakeTile(Glyphs.Moon, L.T("Chế độ tối"), false, t =>
        {
            bool target = !t.On;
            StatusService.SetWindowsDark(target);
            SetTile(t, target, target ? L.T("Bật") : L.T("Tắt"));
            if (Config.Current.Theme.Mode == ThemeMode.System) App.Instance.Dispatcher.BeginInvoke(() => ThemeManager.Apply(false));
        });
        SetTile(darkTile, dark, dark ? L.T("Bật") : L.T("Tắt"));
        var quick = MakeTile(Glyphs.Gear, L.T("Cài đặt nhanh"), false, _ => { CloseRequested?.Invoke(); Native.SendKeys(Native.VK_LWIN, 0x41); });
        SetTile(quick, false, "Win+A");
        grid.Children.Add(wifi.Root); grid.Children.Add(bt.Root); grid.Children.Add(darkTile.Root); grid.Children.Add(quick.Root);
        root.Children.Add(grid);

        async Task RefreshRadios()
        {
            var (w, b) = await StatusService.GetRadios();
            var net = StatusService.Instance.Net;
            if (w == null) SetTile(wifi, net.Kind == NetKind.Wifi, net.Kind == NetKind.Wifi ? net.Name : L.T("Không rõ"));
            else SetTile(wifi, w.Value, w.Value ? (net.Kind == NetKind.Wifi ? net.Name : L.T("Bật")) : L.T("Tắt"));
            if (b == null) SetTile(bt, false, L.T("Không có"));
            else SetTile(bt, b.Value, b.Value ? L.T("Bật") : L.T("Tắt"));
        }
        _ = RefreshRadios();

        // Độ sáng (laptop)
        var brightBox = new StackPanel { Visibility = Visibility.Collapsed };
        root.Children.Add(brightBox);
        Task.Run(StatusService.GetBrightness).ContinueWith(task =>
        {
            int? b = task.Result;
            App.Instance.Dispatcher.BeginInvoke(() =>
            {
                if (b == null) return;
                brightBox.Visibility = Visibility.Visible;
                brightBox.Children.Add(new TextBlock { Text = L.T("Màn hình"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 10, 0, 2) });
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 0) };
                row.Children.Add(Glyphs.Icon(Glyphs.Sun, T.PopupForeground, 16));
                DateTime last = DateTime.MinValue;
                var s = MakeSlider(b.Value, v =>
                {
                    if ((DateTime.Now - last).TotalMilliseconds < 120) return;
                    last = DateTime.Now;
                    int val = (int)v;
                    Task.Run(() => StatusService.SetBrightness(val));
                });
                s.Width = 250; s.Margin = new Thickness(8, 4, 0, 4);
                s.PreviewMouseLeftButtonUp += (_, _) => { int val = (int)s.Value; Task.Run(() => StatusService.SetBrightness(val)); };
                row.Children.Add(s);
                brightBox.Children.Add(row);
            });
        });

        // Âm thanh
        StatusService.Instance.RefreshAudio();
        if (StatusService.Instance.AudioAvailable)
        {
            root.Children.Add(new TextBlock { Text = L.T("Âm thanh"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 10, 0, 2) });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 0) };
            row.Children.Add(Glyphs.VolumeIcon(T.PopupForeground, StatusService.Instance.Volume, false, 16));
            var s = MakeSlider(StatusService.Instance.Volume * 100, v => { AudioVolume.Instance.Level = (float)(v / 100); if (v > 0) AudioVolume.Instance.Muted = false; });
            s.Width = 250; s.Margin = new Thickness(8, 4, 0, 4);
            row.Children.Add(s);
            root.Children.Add(row);
        }

        root.Children.Add(Sep());
        root.Children.Add(Link(L.T("Cài đặt MacDock…"), () => App.Instance.ShowSettings()));
        return root;
    }
}
