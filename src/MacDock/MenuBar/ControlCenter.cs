using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WShapes = System.Windows.Shapes;

namespace MacDock;

/// <summary>
/// Trung tâm điều khiển phong cách "liquid glass": các khối bo tròn trên lưới 4 cột, nút tròn, thanh trượt dày,
/// khối Wi-Fi/Bluetooth mở rộng được, khối Đang phát, và chế độ Sửa để thêm/bớt/sắp xếp/đổi kích thước.
/// </summary>
public sealed class ControlCenter
{
    const double Cell = 72, Gap = 10;
    readonly GlassPopup _popup;
    readonly Grid _root = new() { Width = 4 * Cell + 3 * Gap };

    public ControlCenter(GlassPopup popup) { _popup = popup; }

    public static FrameworkElement Build(GlassPopup popup) => new ControlCenter(popup).Main();
    /// <summary>Bảng Wi-Fi riêng (bấm icon Wi-Fi trên Thanh Menu).</summary>
    public static FrameworkElement BuildWifi(GlassPopup popup) => new ControlCenter(popup) { _standalone = true }.WifiView();
    /// <summary>Bảng Âm thanh riêng: thanh âm lượng + chọn thiết bị phát.</summary>
    public static FrameworkElement BuildSound(GlassPopup popup) => new ControlCenter(popup) { _standalone = true }.SoundView();
    bool _standalone;

    // ------------------------------------------------------------------ danh mục module

    public sealed record ModuleDef(string Id, string Title, bool Button, string? Glyph, Geometry? Vector);

    public static readonly ModuleDef[] All =
    {
        new("connectivity", "Wi-Fi & Bluetooth", false, null, null),
        new("nowplaying", "Đang phát", false, null, null),
        new("display", "Màn hình", false, null, null),
        new("sound", "Âm thanh", false, null, null),
        new("focus", "Không làm phiền", true, "", null),
        new("darkmode", "Chế độ tối", true, null, null),
        new("screenshot", "Chụp màn hình", true, "", null),
        new("airplane", "Chế độ máy bay", true, "", null),
        new("mic", "Micro", true, "", null),
        new("nightlight", "Ánh sáng ban đêm", true, null, null),
        new("hotspot", "Phát sóng", true, "", null),
        new("energy", "Chế độ năng lượng", true, null, null),
        new("cast", "Chiếu màn hình", true, "", null),
        new("calculator", "Máy tính", true, "", null),
        new("accessibility", "Trợ năng", true, "", null),
        new("lock", "Khoá màn hình", true, "", null),
    };

    static List<string> Layout => Config.Current.MenuBar.ControlCenter;

    // ------------------------------------------------------------------ màn hình chính

    FrameworkElement Main()
    {
        var wrap = new WrapPanel { Width = 4 * (Cell + Gap), Margin = new Thickness(-Gap / 2) };
        foreach (var entry in Layout)
        {
            var parts = entry.Split(':');
            var id = parts[0];
            string size = parts.Length > 1 ? parts[1] : "s";
            FrameworkElement? m = id switch
            {
                "connectivity" => Connectivity(),
                "nowplaying" => NowPlayingTile(),
                "display" => DisplayTile(),
                "sound" => SoundTile(),
                _ => ButtonModule(id, size == "m")
            };
            if (m == null) continue;
            m.Margin = new Thickness(Gap / 2);
            wrap.Children.Add(m);
        }
        var edit = Pill(L.T("Sửa điều khiển…"), () => Show(EditView()));
        edit.HorizontalAlignment = HorizontalAlignment.Center;
        edit.Margin = new Thickness(0, 8, 0, 0);
        var sp = new StackPanel { Width = 4 * Cell + 3 * Gap };
        sp.Children.Add(wrap);
        sp.Children.Add(edit);
        return sp;
    }

    void Show(FrameworkElement view)
    {
        _popup.Body = view;
    }

    // ------------------------------------------------------------------ khối dựng sẵn

    static Border Tile(double w, double h, UIElement child, double radius = 18) => new()
    {
        Width = w, Height = h, CornerRadius = new CornerRadius(radius), Background = Glass.Tile,
        BorderBrush = Glass.TileRim, BorderThickness = new Thickness(0.8), Child = child, ClipToBounds = true
    };

    static Border Circle(double d, UIElement icon, bool on)
    {
        var b = new Border { Width = d, Height = d, CornerRadius = new CornerRadius(d / 2), Background = on ? Glass.On : Glass.Off, Child = icon };
        return b;
    }

    static UIElement ModuleIcon(ModuleDef def, double size, Brush fg)
    {
        if (def.Glyph != null) return Glass.Icon(def.Glyph, size, fg);
        Geometry g = def.Id switch
        {
            "darkmode" => HalfCircle(),
            "nightlight" => Glyphs.Sun,
            "energy" => Glyphs.Bolt,
            _ => Glyphs.Info
        };
        return Glyphs.Icon(g, fg, size + 2);
    }

    static Geometry HalfCircle()
    {
        var full = new EllipseGeometry(new Point(8, 8), 6.5, 6.5);
        var ring = Geometry.Combine(full, new EllipseGeometry(new Point(8, 8), 5.2, 5.2), GeometryCombineMode.Exclude, null);
        var half = Geometry.Combine(new EllipseGeometry(new Point(8, 8), 5.2, 5.2), new RectangleGeometry(new Rect(8, 0, 8, 16)), GeometryCombineMode.Exclude, null);
        var g = Geometry.Combine(ring, half, GeometryCombineMode.Union, null);
        g.Freeze();
        return g;
    }

    static FrameworkElement Clickable(FrameworkElement e, Action a)
    {
        e.Cursor = Cursors.Hand;
        if (e is Border b)
        {
            var normal = b.Background;
            e.MouseEnter += (_, _) => { if (b.Background == normal && normal == Glass.Tile) b.Background = Glass.TileHover; };
            e.MouseLeave += (_, _) => { if (b.Background == Glass.TileHover) b.Background = normal; };
        }
        e.MouseLeftButtonUp += (_, ev) => { ev.Handled = true; try { a(); } catch (Exception ex) { Log.Warn("Control Center: " + ex.Message); } };
        return e;
    }

    static Border Pill(string text, Action a)
    {
        var b = new Border
        {
            CornerRadius = new CornerRadius(14), Background = Glass.Tile, BorderBrush = Glass.TileRim, BorderThickness = new Thickness(0.8),
            Padding = new Thickness(14, 5, 14, 5), Child = new TextBlock { Text = text, FontSize = 12.5 }
        };
        Clickable(b, a);
        return b;
    }

    static TextBlock Title(string s, double size = 13) => new() { Text = s, FontWeight = FontWeights.SemiBold, FontSize = size, TextTrimming = TextTrimming.CharacterEllipsis };
    static TextBlock Sub(string s) => new() { Text = s, Foreground = Glass.SubFg, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis };

    // ------------------------------------------------------------------ Wi-Fi & Bluetooth

    FrameworkElement Connectivity()
    {
        var sp = new StackPanel { Margin = new Thickness(12, 10, 10, 10) };
        var net = StatusService.Instance.Net;
        var wifiRow = ConnRow("", "Wi-Fi", net.Kind == NetKind.Wifi ? net.Name : L.T("Không kết nối"), net.Kind == NetKind.Wifi,
            async on => { await StatusService.SetRadio(Windows.Devices.Radios.RadioKind.WiFi, on); }, () => Show(WifiView()));
        var btRow = ConnRow("", "Bluetooth", "…", false,
            async on => { await StatusService.SetRadio(Windows.Devices.Radios.RadioKind.Bluetooth, on); }, () => Show(BluetoothView()));
        var hsRow = ConnRow("", L.T("Phát sóng"), L.T("Cài đặt"), false, _ => { Launcher.Open("ms-settings:network-mobilehotspot"); return Task.CompletedTask; },
            () => Launcher.Open("ms-settings:network-mobilehotspot"));
        sp.Children.Add(wifiRow.root); sp.Children.Add(btRow.root); sp.Children.Add(hsRow.root);
        _ = Task.Run(async () =>
        {
            var (w, b) = await StatusService.GetRadios();
            var devs = b == true ? await BluetoothDevices.PairedAsync() : new List<BtDevice>();
            App.Instance.Dispatcher.Invoke(() =>
            {
                if (w is bool wo) wifiRow.set(wo, wo ? (net.Kind == NetKind.Wifi ? net.Name : L.T("Bật")) : L.T("Tắt"));
                if (b is bool bo)
                {
                    var c = devs.FirstOrDefault(d => d.Connected);
                    btRow.set(bo, bo ? (c?.Name ?? L.T("Bật")) : L.T("Tắt"));
                }
                else btRow.set(false, L.T("Không có"));
            });
        });
        return Tile(2 * Cell + Gap, 2 * Cell + Gap, sp, 22);
    }

    (FrameworkElement root, Action<bool, string> set) ConnRow(string glyph, string name, string status, bool on, Func<bool, Task> toggle, Action open)
    {
        bool state = on;
        var icon = Glass.Icon(glyph, 14, on ? Brushes.White : Glass.Fg);
        var circle = Circle(32, icon, on);
        var sub = Sub(status);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9, 0, 0, 0), Width = 2 * Cell + Gap - 32 - 9 - 24 };
        text.Children.Add(Title(name, 12.5)); text.Children.Add(sub);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5), Background = Brushes.Transparent };
        row.Children.Add(circle); row.Children.Add(text);
        void Set(bool v, string s) { state = v; circle.Background = v ? Glass.On : Glass.Off; icon.Foreground = v ? Brushes.White : Glass.Fg; sub.Text = s; }
        Clickable(circle, async () => { Set(!state, !state ? L.T("Đang bật…") : L.T("Đang tắt…")); await toggle(state); });
        Clickable(text, open);
        return (row, Set);
    }

    // ------------------------------------------------------------------ Đang phát

    FrameworkElement NowPlayingTile()
    {
        var art = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(10), Background = Glass.Off, Child = Glass.Icon("", 20, Glass.SubFg), ClipToBounds = true };
        var title = Title(L.T("Chưa phát"), 12.5);
        var artist = Sub("");
        var play = Glass.Icon("", 18);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        FrameworkElement Btn(UIElement ic, Func<Task> a) => Clickable(new Border { Width = 40, Height = 30, Background = Brushes.Transparent, Child = ic }, async () => { await a(); await Task.Delay(400); await Refresh(); });
        controls.Children.Add(Btn(Glass.Icon("", 15), NowPlaying.Previous));
        controls.Children.Add(Btn(play, NowPlaying.Toggle));
        controls.Children.Add(Btn(Glass.Icon("", 15), NowPlaying.Next));
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(art);
        var tx = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        tx.Children.Add(title); tx.Children.Add(artist);
        sp.Children.Add(tx);
        sp.Children.Add(controls);
        async Task Refresh()
        {
            var np = await NowPlaying.GetAsync();
            if (np == null) return;
            title.Text = string.IsNullOrWhiteSpace(np.Title) ? L.T("Chưa phát") : np.Title;
            artist.Text = np.Artist;
            play.Text = np.Playing ? "" : "";
            if (np.Art != null) art.Child = new Image { Source = np.Art, Stretch = Stretch.UniformToFill };
        }
        _ = Refresh();
        return Tile(2 * Cell + Gap, 2 * Cell + Gap, sp, 22);
    }

    // ------------------------------------------------------------------ thanh trượt dày

    static FrameworkElement ThickSlider(UIElement icon, double value, Action<double> changed, double width)
    {
        const double h = 28;
        var fill = new Border { CornerRadius = new CornerRadius(h / 2), Background = Glass.SliderFill, HorizontalAlignment = HorizontalAlignment.Left, Height = h };
        var track = new Grid { Width = width, Height = h, Background = Brushes.Transparent, Cursor = Cursors.Hand };
        track.Children.Add(new Border { CornerRadius = new CornerRadius(h / 2), Background = Glass.Track });
        track.Children.Add(fill);
        var ic = new Border { Child = icon, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(9, 0, 0, 0), IsHitTestVisible = false };
        track.Children.Add(ic);
        void Set(double v)
        {
            v = Math.Clamp(v, 0, 1);
            fill.Width = Math.Max(h, h + (width - h) * v);
        }
        Set(value);
        void FromMouse(MouseEventArgs e)
        {
            double v = (e.GetPosition(track).X - h / 2) / (width - h);
            v = Math.Clamp(v, 0, 1);
            Set(v);
            changed(v);
        }
        track.MouseLeftButtonDown += (_, e) => { track.CaptureMouse(); FromMouse(e); e.Handled = true; };
        track.MouseMove += (_, e) => { if (track.IsMouseCaptured) FromMouse(e); };
        track.MouseLeftButtonUp += (_, e) => { track.ReleaseMouseCapture(); e.Handled = true; };
        track.MouseWheel += (_, e) =>
        {
            double cur = (fill.Width - h) / (width - h);
            double v = Math.Clamp(cur + (e.Delta > 0 ? 0.05 : -0.05), 0, 1);
            Set(v); changed(v); e.Handled = true;
        };
        return track;
    }

    FrameworkElement DisplayTile()
    {
        var sp = new StackPanel { Margin = new Thickness(14, 10, 14, 12) };
        sp.Children.Add(Title(L.T("Màn hình")));
        var holder = new ContentControl { Margin = new Thickness(0, 8, 0, 0) };
        holder.Content = new TextBlock { Text = "…", Foreground = Glass.SubFg };
        sp.Children.Add(holder);
        _ = Task.Run(StatusService.GetBrightness).ContinueWith(t => App.Instance.Dispatcher.BeginInvoke(() =>
        {
            if (t.Result is int b)
            {
                DateTime last = DateTime.MinValue;
                holder.Content = ThickSlider(Glyphs.Icon(Glyphs.Sun, Brushes.Gray, 14), b / 100.0, v =>
                {
                    if ((DateTime.Now - last).TotalMilliseconds < 90) return;
                    last = DateTime.Now; int val = (int)Math.Round(v * 100);
                    Task.Run(() => StatusService.SetBrightness(val));
                }, 4 * Cell + 3 * Gap - 28);
            }
            else holder.Content = Clickable(new TextBlock { Text = L.T("Màn hình này không chỉnh độ sáng được — mở Cài đặt màn hình"), Foreground = Glass.SubFg, TextWrapping = TextWrapping.Wrap }, () => Launcher.Open("ms-settings:display"));
        }));
        return Tile(4 * Cell + 3 * Gap, double.NaN, sp, 20);
    }

    FrameworkElement SoundTile()
    {
        var sp = new StackPanel { Margin = new Thickness(14, 10, 14, 12) };
        var head = new DockPanel();
        var more = Clickable(new Border { Width = 26, Height = 20, Background = Brushes.Transparent, Child = Glass.Icon("", 11, Glass.SubFg), ToolTip = L.T("Thiết bị phát") }, () => Show(SoundView()));
        DockPanel.SetDock(more, Dock.Right);
        head.Children.Add(more);
        head.Children.Add(Title(L.T("Âm thanh")));
        sp.Children.Add(head);
        StatusService.Instance.RefreshAudio();
        var slider = ThickSlider(Glass.Icon("", 13, Brushes.Gray), AudioVolume.Instance.Muted ? 0 : StatusService.Instance.Volume, v =>
        {
            AudioVolume.Instance.Level = (float)v;
            if (v > 0 && AudioVolume.Instance.Muted) AudioVolume.Instance.Muted = false;
        }, 4 * Cell + 3 * Gap - 28);
        slider.Margin = new Thickness(0, 8, 0, 0);
        sp.Children.Add(slider);
        return Tile(4 * Cell + 3 * Gap, double.NaN, sp, 20);
    }

    // ------------------------------------------------------------------ nút

    FrameworkElement? ButtonModule(string id, bool medium)
    {
        var def = All.FirstOrDefault(d => d.Id == id);
        if (def == null || !def.Button) return null;
        bool on = id switch
        {
            "darkmode" => ThemeManager.SystemIsDark(),
            "mic" => AudioDevices.MicMuted() == true,
            _ => false
        };
        var iconFg = on ? Brushes.White : Glass.Fg;
        var icon = ModuleIcon(def, 17, iconFg);
        var circle = Circle(medium ? 34 : 46, icon, on);
        Action act = id switch
        {
            "darkmode" => () => { bool v = !ThemeManager.SystemIsDark(); StatusService.SetWindowsDark(v); Repaint(circle, v, def); },
            "mic" => () => { bool? m = AudioDevices.MicMuted(!(AudioDevices.MicMuted() ?? false)); Repaint(circle, m == true, def); },
            "screenshot" => () => { _popup.Close(); Task.Delay(250).ContinueWith(_ => Native.SendKeys(Native.VK_LWIN, Native.VK_SHIFT, 0x53)); },
            "airplane" => async () =>
            {
                var (w, b) = await StatusService.GetRadios();
                bool turnOff = (w ?? false) || (b ?? false);
                await StatusService.SetRadio(Windows.Devices.Radios.RadioKind.WiFi, !turnOff);
                await StatusService.SetRadio(Windows.Devices.Radios.RadioKind.Bluetooth, !turnOff);
                Repaint(circle, turnOff, def);
            },
            "focus" => () => Launcher.Open("ms-settings:quiethours"),
            "nightlight" => () => Launcher.Open("ms-settings:nightlight"),
            "hotspot" => () => Launcher.Open("ms-settings:network-mobilehotspot"),
            "energy" => () => Launcher.Open("ms-settings:batterysaver"),
            "cast" => () => { _popup.Close(); Native.SendKeys(Native.VK_LWIN, 0x4B); },
            "calculator" => () => { _popup.Close(); Launcher.Launch("aumid", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"); },
            "accessibility" => () => Launcher.Open("ms-settings:easeofaccess"),
            "lock" => () => { _popup.Close(); PowerActions.Lock(); },
            _ => () => { }
        };
        if (id == "airplane") _ = StatusService.GetRadios().ContinueWith(t => App.Instance.Dispatcher.BeginInvoke(() => Repaint(circle, t.Result.wifi == false && t.Result.bt != true, def)));

        if (medium)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(circle);
            var tb = new TextBlock { Text = L.T(def.Title), FontWeight = FontWeights.SemiBold, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9, 0, 0, 0), TextWrapping = TextWrapping.Wrap, Width = 2 * Cell + Gap - 34 - 9 - 22 };
            row.Children.Add(tb);
            var t = Tile(2 * Cell + Gap, Cell, row, 22);
            return Clickable(t, act);
        }
        var sp = new StackPanel { Width = Cell, Height = Cell };
        sp.Children.Add(circle);
        sp.Children.Add(new TextBlock { Text = L.T(def.Title), FontSize = 10.5, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0), Foreground = Glass.Fg });
        circle.HorizontalAlignment = HorizontalAlignment.Center;
        Clickable(circle, act);
        return sp;
    }

    static void Repaint(Border circle, bool on, ModuleDef def)
    {
        circle.Background = on ? Glass.On : Glass.Off;
        circle.Child = ModuleIcon(def, 17, on ? Brushes.White : Glass.Fg);
    }

    // ------------------------------------------------------------------ màn hình chi tiết

    FrameworkElement DetailFrame(string title, UIElement? headerRight, out StackPanel body, string? settingsText = null, string? settingsUri = null)
    {
        var sp = new StackPanel { Width = 4 * Cell + 3 * Gap };
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var back = Clickable(new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Background = Glass.Tile, Child = Glass.Icon("", 12), Margin = new Thickness(0, 0, 8, 0) }, () => Show(Main()));
        DockPanel.SetDock(back, Dock.Left);
        if (!_standalone) head.Children.Add(back);
        if (headerRight != null) { DockPanel.SetDock(headerRight, Dock.Right); head.Children.Add(headerRight); }
        var tt = Title(title, 15); tt.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(tt);
        sp.Children.Add(head);
        body = new StackPanel();
        var scroll = new ScrollViewer { Content = body, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sp.Children.Add(Tile(4 * Cell + 3 * Gap, double.NaN, new Border { Padding = new Thickness(8), Child = scroll }, 18));
        if (settingsText != null)
        {
            var s = Pill(settingsText, () => { _popup.Close(); Launcher.Open(settingsUri!); });
            s.HorizontalAlignment = HorizontalAlignment.Left;
            s.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(s);
        }
        return sp;
    }

    static FrameworkElement ListRow(UIElement icon, string name, string? status, bool active, Action? click, UIElement? right = null)
    {
        var dp = new DockPanel { Margin = new Thickness(0) };
        var circle = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Background = active ? Glass.On : Glass.Off, Child = icon, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(circle, Dock.Left);
        dp.Children.Add(circle);
        if (right != null) { DockPanel.SetDock(right, Dock.Right); dp.Children.Add(right); }
        var tx = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        tx.Children.Add(new TextBlock { Text = name, FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis });
        if (!string.IsNullOrEmpty(status)) tx.Children.Add(Sub(status));
        dp.Children.Add(tx);
        var b = new Border { Child = dp, Padding = new Thickness(6, 5, 6, 5), CornerRadius = new CornerRadius(10), Background = Brushes.Transparent };
        if (click != null)
        {
            b.Cursor = Cursors.Hand;
            b.MouseEnter += (_, _) => b.Background = Glass.TileHover;
            b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonUp += (_, e) => { e.Handled = true; click(); };
        }
        return b;
    }

    static CheckBox Switch(bool value, Action<bool> changed)
    {
        var cb = new CheckBox { IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
        cb.SetResourceReference(FrameworkElement.StyleProperty, "Md.Toggle");
        cb.Checked += (_, _) => changed(true);
        cb.Unchecked += (_, _) => changed(false);
        return cb;
    }

    public FrameworkElement WifiView()
    {
        var sw = Switch(StatusService.Instance.Net.Kind == NetKind.Wifi, async v =>
        {
            await StatusService.SetRadio(Windows.Devices.Radios.RadioKind.WiFi, v);
            await Task.Delay(1500);
            Show(WifiView());
        });
        var frame = DetailFrame("Wi-Fi", sw, out var body, L.T("Cài đặt Wi-Fi…"), "ms-settings:network-wifi");
        var status = Sub(L.T("Đang tìm mạng…"));
        body.Children.Add(status);
        bool busy = false;
        async void Load(bool scan)
        {
            var nets = await Task.Run(() => Wlan.Networks(scan));
            body.Children.Clear();
            if (nets.Count == 0) { body.Children.Add(Sub(L.T("Không tìm thấy mạng Wi-Fi nào (Wi-Fi có thể đang tắt)."))); return; }
            void Section(string name, IEnumerable<WifiNetwork> items)
            {
                var list = items.ToList();
                if (list.Count == 0) return;
                body.Children.Add(new TextBlock { Text = name, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Glass.SubFg, Margin = new Thickness(6, 6, 0, 3) });
                foreach (var n in list)
                {
                    var net = n;
                    var lockIcon = n.Secured ? Glass.Icon("", 11, Glass.SubFg) : null;
                    body.Children.Add(ListRow(Glass.Icon("", 13, n.Connected ? Brushes.White : Glass.Fg), n.Ssid,
                        n.Connected ? L.T("Đã kết nối — bấm để ngắt") : null, n.Connected, async () =>
                        {
                            if (busy) return;
                            if (net.Connected) { Wlan.Disconnect(net); await Task.Delay(1200); Load(false); return; }
                            string? pass = null;
                            if (net.Secured && net.Profile == null)
                            {
                                pass = WifiPasswordWindow.Ask(net.Ssid);
                                if (pass == null) return;
                            }
                            busy = true;
                            body.Children.Insert(0, Sub(L.T("Đang kết nối ") + net.Ssid + "…"));
                            var err = await Task.Run(() => Wlan.Connect(net, pass));
                            if (err != null) { busy = false; body.Children.Insert(0, Sub(err)); return; }
                            for (int i = 0; i < 10; i++)
                            {
                                await Task.Delay(800);
                                var q = await Task.Run(Wlan.Query);
                                if (q.Kind == NetKind.Wifi && q.Name == net.Ssid) { StatusService.Instance.RefreshAll(); break; }
                            }
                            busy = false; Load(false);
                        }, lockIcon));
                }
            }
            Section(L.T("Mạng đã biết"), nets.Where(x => x.Connected || x.Profile != null));
            Section(L.T("Mạng khác"), nets.Where(x => !(x.Connected || x.Profile != null)));
        }
        Load(false);
        Task.Delay(200).ContinueWith(_ => App.Instance.Dispatcher.BeginInvoke(() => Load(true)));
        return frame;
    }

    FrameworkElement BluetoothView()
    {
        var sw = Switch(false, async v =>
        {
            await StatusService.SetRadio(Windows.Devices.Radios.RadioKind.Bluetooth, v);
            await Task.Delay(1200);
            Show(BluetoothView());
        });
        _ = StatusService.GetRadios().ContinueWith(t => App.Instance.Dispatcher.BeginInvoke(() => { if (t.Result.bt is bool b) sw.IsChecked = b; }));
        var frame = DetailFrame("Bluetooth", sw, out var body, L.T("Cài đặt Bluetooth…"), "ms-settings:bluetooth");
        body.Children.Add(Sub(L.T("Đang tải thiết bị…")));
        _ = BluetoothDevices.PairedAsync().ContinueWith(t => App.Instance.Dispatcher.BeginInvoke(() =>
        {
            body.Children.Clear();
            var list = t.Result;
            if (list.Count == 0) { body.Children.Add(Sub(L.T("Chưa có thiết bị nào được ghép đôi."))); return; }
            body.Children.Add(new TextBlock { Text = L.T("Thiết bị của tôi"), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Glass.SubFg, Margin = new Thickness(6, 4, 0, 3) });
            foreach (var d in list)
                body.Children.Add(ListRow(Glass.Icon("", 13, d.Connected ? Brushes.White : Glass.Fg), d.Name, d.Connected ? L.T("Đã kết nối") : L.T("Chưa kết nối"), d.Connected,
                    () => { _popup.Close(); Launcher.Open("ms-settings:bluetooth"); }));
        }));
        return frame;
    }

    FrameworkElement SoundView()
    {
        var frame = DetailFrame(L.T("Âm thanh"), null, out var body, L.T("Cài đặt âm thanh…"), "ms-settings:sound");
        if (_standalone)
        {
            StatusService.Instance.RefreshAudio();
            var vol = ThickSlider(Glass.Icon("\uE767", 13, Brushes.Gray), AudioVolume.Instance.Muted ? 0 : StatusService.Instance.Volume, v =>
            {
                AudioVolume.Instance.Level = (float)v;
                if (v > 0 && AudioVolume.Instance.Muted) AudioVolume.Instance.Muted = false;
            }, 4 * Cell + 3 * Gap - 16);
            vol.Margin = new Thickness(0, 2, 0, 10);
            ((StackPanel)frame).Children.Insert(1, vol);
        }
        body.Children.Add(new TextBlock { Text = L.T("Đầu ra"), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Glass.SubFg, Margin = new Thickness(6, 4, 0, 3) });
        foreach (var d in AudioDevices.List(0))
        {
            var dev = d;
            body.Children.Add(ListRow(Glass.Icon("", 13, d.IsDefault ? Brushes.White : Glass.Fg), d.Name, null, d.IsDefault,
                () => { AudioDevices.SetDefault(dev.Id); StatusService.Instance.RefreshAll(); Show(SoundView()); },
                d.IsDefault ? Glass.Icon("", 12) : null));
        }
        var mics = AudioDevices.List(1);
        if (mics.Count > 0)
        {
            body.Children.Add(new TextBlock { Text = L.T("Đầu vào"), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Glass.SubFg, Margin = new Thickness(6, 8, 0, 3) });
            foreach (var d in mics)
            {
                var dev = d;
                body.Children.Add(ListRow(Glass.Icon("", 13, d.IsDefault ? Brushes.White : Glass.Fg), d.Name, null, d.IsDefault,
                    () => { AudioDevices.SetDefault(dev.Id); Show(SoundView()); }, d.IsDefault ? Glass.Icon("", 12) : null));
            }
        }
        return frame;
    }

    // ------------------------------------------------------------------ chế độ Sửa

    FrameworkElement EditView()
    {
        var frame = DetailFrame(L.T("Sửa điều khiển"), null, out var body);
        { var hint = Sub(L.T("Bật để thêm vào Trung tâm điều khiển; dùng mũi tên để sắp xếp, S/M để đổi kích thước nút.")); hint.TextWrapping = TextWrapping.Wrap; hint.TextTrimming = TextTrimming.None; hint.Margin = new Thickness(6, 0, 6, 6); body.Children.Add(hint); }
        var layout = Layout;
        IEnumerable<ModuleDef> ordered = layout.Select(e => All.FirstOrDefault(d => d.Id == e.Split(':')[0])).Where(d => d != null).Cast<ModuleDef>()
            .Concat(All.Where(d => !layout.Any(e => e.Split(':')[0] == d.Id)));
        foreach (var def in ordered)
        {
            var d = def;
            int idx = layout.FindIndex(e => e.Split(':')[0] == d.Id);
            bool shown = idx >= 0;
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            FrameworkElement Mini(string text, Action a, bool enabled = true)
            {
                var b = new Border { Width = 24, Height = 22, CornerRadius = new CornerRadius(6), Background = Glass.Off, Margin = new Thickness(3, 0, 0, 0), Child = new TextBlock { Text = text, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, Opacity = enabled ? 1 : 0.35 };
                if (enabled) Clickable(b, a);
                return b;
            }
            if (shown)
            {
                if (d.Button)
                {
                    bool med = layout[idx].EndsWith(":m");
                    right.Children.Add(Mini(med ? "M" : "S", () => { layout[idx] = d.Id + (med ? ":s" : ":m"); Save(); }));
                }
                right.Children.Add(Mini("↑", () => { var x = layout[idx]; layout.RemoveAt(idx); layout.Insert(idx - 1, x); Save(); }, idx > 0));
                right.Children.Add(Mini("↓", () => { var x = layout[idx]; layout.RemoveAt(idx); layout.Insert(idx + 1, x); Save(); }, idx < layout.Count - 1));
            }
            var tg = Switch(shown, v =>
            {
                if (v) layout.Add(d.Id + (d.Button ? ":s" : ""));
                else layout.RemoveAll(e => e.Split(':')[0] == d.Id);
                Save();
            });
            tg.Margin = new Thickness(8, 0, 0, 0);
            right.Children.Add(tg);
            UIElement icon = d.Button ? ModuleIcon(d, 13, Glass.Fg) : Glass.Icon(d.Id switch { "connectivity" => "", "nowplaying" => "", "display" => "", _ => "" }, 13);
            body.Children.Add(ListRow(icon, L.T(d.Title), null, false, null, right));
        }
        var done = Pill(L.T("Xong"), () => Show(Main()));
        done.HorizontalAlignment = HorizontalAlignment.Right;
        done.Margin = new Thickness(0, 10, 0, 0);
        ((StackPanel)frame).Children.Add(done);
        return frame;

        void Save()
        {
            Config.Touch("controlcenter");
            Show(EditView());
        }
    }
}
