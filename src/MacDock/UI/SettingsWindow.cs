using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WShapes = System.Windows.Shapes;

namespace MacDock;

/// <summary>Cửa sổ Cài đặt kiểu System Settings, mọi thay đổi áp dụng ngay (xem trước trực tiếp).</summary>
public sealed class SettingsWindow : Window
{
    readonly StackPanel _nav = new() { Margin = new Thickness(10, 6, 10, 10) };
    readonly ScrollViewer _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    readonly Dictionary<string, Border> _navItems = new();
    string _page = "general";
    DispatcherTimer? _perfTimer;

    static ResolvedTheme T => ThemeManager.Current;
    static AppConfig C => Config.Current;

    record PageDef(string Id, string Title, Geometry Glyph, Color Color, bool Stroke = false);

    static readonly PageDef[] Pages =
    {
        new("general", "Chung", Glyphs.Gear, Color.FromRgb(0x8E, 0x8E, 0x93)),
        new("dock", "Dock", Glyphs.DockShape, Color.FromRgb(0x0A, 0x84, 0xFF)),
        new("menubar", "Thanh Menu", Glyphs.MenuBarShape, Color.FromRgb(0x5E, 0x5C, 0xE6)),
        new("theme", "Giao diện", Glyphs.Palette, Color.FromRgb(0xFF, 0x9F, 0x0A)),
        new("icons", "Biểu tượng", Glyphs.Grid, Color.FromRgb(0x30, 0xD1, 0x58)),
        new("performance", "Hiệu năng", Glyphs.Bolt, Color.FromRgb(0xFF, 0x37, 0x5F)),
        new("about", "Giới thiệu", Glyphs.Info, Color.FromRgb(0x64, 0xD2, 0xFF)),
    };

    public SettingsWindow()
    {
        Title = "Cài đặt MacDock";
        Width = 900; Height = 640; MinWidth = 720; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = Glyphs.AppIcon;
        ShowInTaskbar = true;
        SetResourceReference(BackgroundProperty, "Md.WindowBg");
        SetResourceReference(ForegroundProperty, "Md.PopupFg");
        SetResourceReference(FontFamilyProperty, "Md.Font");
        FontSize = 13;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var side = new Border { BorderThickness = new Thickness(0, 0, 1, 0) };
        side.SetResourceReference(Border.BorderBrushProperty, "Md.Separator");
        side.SetResourceReference(Border.BackgroundProperty, "Md.CardBg");
        var sideStack = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 18, 10, 12) };
        head.Children.Add(new Image { Source = Glyphs.AppIcon, Width = 34, Height = 34 });
        var ht = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        ht.Children.Add(new TextBlock { Text = "MacDock", FontSize = 16, FontWeight = FontWeights.SemiBold });
        ht.Children.Add(Subtle("Phiên bản " + App.VersionString));
        head.Children.Add(ht);
        sideStack.Children.Add(head);
        foreach (var p in Pages) _nav.Children.Add(NavItem(p));
        sideStack.Children.Add(_nav);
        side.Child = sideStack;
        grid.Children.Add(side);

        Grid.SetColumn(_scroll, 1);
        grid.Children.Add(_scroll);
        Content = grid;

        ThemeManager.Changed += OnTheme;
        Closed += (_, _) => { ThemeManager.Changed -= OnTheme; _perfTimer?.Stop(); };
        Navigate("general");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var h = new WindowInteropHelper(this).Handle;
        Native.AddExStyle(h, Native.WS_EX_APPWINDOW);
        ApplyTitleBar(h);
    }

    void ApplyTitleBar(IntPtr h)
    {
        int dark = T.IsDark ? 1 : 0;
        Native.DwmSetWindowAttribute(h, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
    }

    void OnTheme()
    {
        var h = new WindowInteropHelper(this).Handle;
        if (h != IntPtr.Zero) ApplyTitleBar(h);
        if (_page == "theme") Navigate(_page, keepScroll: true);
    }

    // ------------------------------------------------------------------ khung

    Border NavItem(PageDef p)
    {
        var icon = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(p.Color), Child = Glyphs.Icon(p.Glyph, Brushes.White, 13, p.Stroke) };
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(icon);
        var tb = new TextBlock { Text = p.Title, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(tb);
        var b = new Border { Child = sp, Padding = new Thickness(8, 6, 8, 6), CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 1, 0, 1), Background = Brushes.Transparent, Cursor = Cursors.Hand };
        b.MouseLeftButtonUp += (_, _) => Navigate(p.Id);
        _navItems[p.Id] = b;
        return b;
    }

    public void Navigate(string page, bool keepScroll = false)
    {
        _perfTimer?.Stop();
        double offset = _scroll.VerticalOffset;
        _page = page;
        foreach (var (id, b) in _navItems)
        {
            bool sel = id == page;
            b.Background = sel ? T.Selection : Brushes.Transparent;
            if (b.Child is StackPanel sp && sp.Children[1] is TextBlock tb) tb.Foreground = sel ? T.SelectionForeground : T.PopupForeground;
        }
        var content = new StackPanel { Margin = new Thickness(28, 22, 28, 28), MaxWidth = 760 };
        var def = Pages.First(p => p.Id == page);
        content.Children.Add(new TextBlock { Text = def.Title, FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 14) });
        switch (page)
        {
            case "general": BuildGeneral(content); break;
            case "dock": BuildDock(content); break;
            case "menubar": BuildMenuBar(content); break;
            case "theme": BuildTheme(content); break;
            case "icons": BuildIcons(content); break;
            case "performance": BuildPerformance(content); break;
            case "about": BuildAbout(content); break;
        }
        _scroll.Content = content;
        if (keepScroll) Dispatcher.BeginInvoke(() => _scroll.ScrollToVerticalOffset(offset), DispatcherPriority.Loaded);
        else _scroll.ScrollToTop();
    }

    // ------------------------------------------------------------------ phần tử dựng sẵn

    static TextBlock Subtle(string s) { var t = new TextBlock { Text = s, FontSize = 12, TextWrapping = TextWrapping.Wrap }; t.SetResourceReference(TextBlock.ForegroundProperty, "Md.SubtleFg"); return t; }

    static StackPanel Card(Panel parent, string? header = null)
    {
        if (header != null) parent.Children.Add(new TextBlock { Text = header, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 14, 0, 6) });
        var stack = new StackPanel();
        var b = new Border { Child = stack, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(14, 2, 14, 2) };
        b.SetResourceReference(Border.BackgroundProperty, "Md.CardBg");
        b.SetResourceReference(Border.BorderBrushProperty, "Md.Separator");
        parent.Children.Add(b);
        return stack;
    }

    static void Row(StackPanel card, string label, UIElement control, string? desc = null)
    {
        if (card.Children.Count > 0)
        {
            var line = new Border { Height = 1 };
            line.SetResourceReference(Border.BackgroundProperty, "Md.Separator");
            card.Children.Add(line);
        }
        var g = new Grid { Margin = new Thickness(0, 9, 0, 9), MinHeight = 24 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        left.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap });
        if (desc != null) left.Children.Add(Subtle(desc));
        g.Children.Add(left);
        if (control is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        g.Children.Add(control);
        card.Children.Add(g);
    }

    static CheckBox Toggle(bool value, Action<bool> set)
    {
        var cb = new CheckBox { IsChecked = value };
        cb.SetResourceReference(StyleProperty, "Md.Toggle");
        cb.Checked += (_, _) => set(true);
        cb.Unchecked += (_, _) => set(false);
        return cb;
    }

    static FrameworkElement SliderBox(double min, double max, double value, Func<double, string> fmt, Action<double> set, double step = 1)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var label = new TextBlock { Text = fmt(value), Width = 64, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        var s = new Slider { Minimum = min, Maximum = max, Value = value, Width = 220, SmallChange = step, LargeChange = step * 5, TickFrequency = step, IsSnapToTickEnabled = step > 0 };
        s.ValueChanged += (_, e) => { label.Text = fmt(e.NewValue); set(e.NewValue); };
        sp.Children.Add(label);
        sp.Children.Add(s);
        return sp;
    }

    static ComboBox Combo<TV>(IEnumerable<(TV value, string text)> items, TV current, Action<TV> set)
    {
        var cb = new ComboBox { MinWidth = 180 };
        foreach (var (v, t) in items) cb.Items.Add(new ComboBoxItem { Content = t, Tag = v });
        foreach (ComboBoxItem it in cb.Items) if (Equals(it.Tag, current)) cb.SelectedItem = it;
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is ComboBoxItem it && it.Tag is TV v) set(v); };
        return cb;
    }

    static Button Btn(string text, Action a, bool accent = false)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0) };
        b.SetResourceReference(StyleProperty, accent ? "Md.AccentButton" : "Md.Button");
        b.Click += (_, _) => { try { a(); } catch (Exception ex) { MessageBox.Show(ex.Message, "MacDock", MessageBoxButton.OK, MessageBoxImage.Warning); } };
        return b;
    }

    static WrapPanel Buttons(params Button[] buttons)
    {
        var wp = new WrapPanel { Margin = new Thickness(2, 12, 0, 0) };
        foreach (var b in buttons) { b.Margin = new Thickness(0, 0, 8, 8); wp.Children.Add(b); }
        return wp;
    }

    void ResetButton(Panel content, string section, string label = "Khôi phục mặc định trang này")
    {
        content.Children.Add(Buttons(Btn(label, () =>
        {
            Config.ResetSection(section);
            Dispatcher.BeginInvoke(() => Navigate(_page, true), DispatcherPriority.Background);
        })));
    }

    static string CopyToIcons(string file)
    {
        try
        {
            var dir = Path.Combine(Paths.IconsDir, "custom"); Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, Guid.NewGuid().ToString("N")[..8] + Path.GetExtension(file));
            File.Copy(file, dest, true);
            return dest;
        }
        catch { return file; }
    }

    // ------------------------------------------------------------------ trang Chung

    void BuildGeneral(StackPanel content)
    {
        var g = C.General;
        var card = Card(content);
        Row(card, "Khởi động cùng Windows", Toggle(g.AutoStart, v => { g.AutoStart = v; Config.Touch("general"); }), "Tự chạy MacDock khi đăng nhập (HKCU\\…\\Run)");
        Row(card, "Ẩn thanh tác vụ của Windows", Toggle(g.HideTaskbar, v => { g.HideTaskbar = v; Config.Touch("general"); }),
            "Tắt nếu muốn giữ taskbar gốc (phương án dự phòng). Khi ẩn, icon khay hệ thống được hiện trên Thanh Menu.");
        Row(card, "Tiến trình canh chừng (watchdog)", Toggle(g.Watchdog, v => { g.Watchdog = v; Config.Touch("general"); }),
            "Tự khôi phục taskbar nếu MacDock bị tắt đột ngột. Có hiệu lực từ lần khởi động sau.");

        var osd = Card(content, "Âm lượng & độ sáng");
        Row(osd, "Giao diện khi bấm phím âm lượng/độ sáng", Combo(new[] { ("macdock", "Kiểu macOS (MacDock)"), ("windows", "Mặc định của Windows") }, g.OsdStyle, v => { g.OsdStyle = v; Config.Touch("general"); }),
            "Phím độ sáng do phần cứng xử lý nên Windows có thể vẫn hiện bảng của nó");
        Row(osd, "Vị trí", Combo(new[] { ("top-right", "Góc trên bên phải (macOS 26)"), ("bottom", "Giữa phía dưới") }, g.OsdPosition, v => { g.OsdPosition = v; Config.Touch("general"); }));
        Row(osd, "Thử", Btn("Hiện thử", () => OsdWindow.ShowLevel(OsdKind.Volume, AudioVolume.Instance.Level, AudioVolume.Instance.Muted)));

        var card2 = Card(content, "Khôi phục & dữ liệu");
        Row(card2, "Khôi phục taskbar Windows ngay", Btn("Khôi phục", () => { g.HideTaskbar = false; Config.Touch("general"); Navigate("general", true); }),
            "Cũng có thể chạy lệnh: MacDock.exe --restore-taskbar");
        Row(card2, "Thư mục dữ liệu", Btn("Mở thư mục", () => Launcher.Open(Paths.Root)), Paths.Root);
        Row(card2, "Xem lại màn hình chào", Btn("Mở", () => new WelcomeWindow(true).Show()));

        content.Children.Add(Buttons(
            Btn("Khôi phục tất cả mặc định", () =>
            {
                if (MessageBox.Show("Đặt lại mọi cài đặt về mặc định? (Danh sách ứng dụng ghim được giữ nguyên.)", "MacDock", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                Config.ResetSection("all");
                Dispatcher.BeginInvoke(() => Navigate("general"), DispatcherPriority.Background);
            }),
            Btn("Thoát MacDock", () => App.Instance.Quit())));
    }

    // ------------------------------------------------------------------ trang Dock

    void BuildDock(StackPanel content)
    {
        var d = C.Dock;
        void T(string s = "dock") => Config.Touch(s);
        var card = Card(content);
        Row(card, "Vị trí trên màn hình", Combo(new[] { (DockEdge.Bottom, "Dưới"), (DockEdge.Left, "Trái"), (DockEdge.Right, "Phải") }, d.Edge, v => { d.Edge = v; T(); }));
        Row(card, "Kích thước", SliderBox(24, 96, d.IconSize, v => $"{v:0} px", v => { d.IconSize = v; T(); }));
        Row(card, "Khoảng cách giữa icon", SliderBox(0, 20, d.Spacing, v => $"{v:0} px", v => { d.Spacing = v; T(); }));
        Row(card, "Hiện trên màn hình", Combo(new[] { (MonitorMode.All, "Tất cả màn hình"), (MonitorMode.Primary, "Chỉ màn hình chính") }, d.Monitors, v => { d.Monitors = v; T(); }));

        var pins = Card(content, "Ứng dụng trên Dock");
        for (int i = 0; i < d.Pinned.Count; i++)
        {
            int idx = i; var pin = d.Pinned[i];
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { Orientation = Orientation.Horizontal };
            var dockItem = DockModel.Instance.Items.FirstOrDefault(x => x.Key == pin.Key);
            var img = dockItem != null ? DockModel.Instance.GetIcon(dockItem) : null;
            left.Children.Add(new Image { Source = img, Width = 24, Height = 24, Margin = new Thickness(0, 0, 10, 0) });
            left.Children.Add(new TextBlock { Text = pin.Name ?? dockItem?.Name ?? Path.GetFileNameWithoutExtension(pin.Path), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 300 });
            row.Children.Add(left);
            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            Button Small(string s, Action a, bool enabled = true) { var b = Btn(s, a); b.Padding = new Thickness(9, 3, 9, 3); b.Margin = new Thickness(4, 0, 0, 0); b.IsEnabled = enabled; return b; }
            btns.Children.Add(Small("↑", () => { var x = d.Pinned[idx]; d.Pinned.RemoveAt(idx); d.Pinned.Insert(idx - 1, x); Config.Touch("pins"); Navigate("dock", true); }, idx > 0));
            btns.Children.Add(Small("↓", () => { var x = d.Pinned[idx]; d.Pinned.RemoveAt(idx); d.Pinned.Insert(idx + 1, x); Config.Touch("pins"); Navigate("dock", true); }, idx < d.Pinned.Count - 1));
            btns.Children.Add(Small("Gỡ", () => { d.Pinned.RemoveAt(idx); Config.Touch("pins"); Navigate("dock", true); }));
            Grid.SetColumn(btns, 1);
            row.Children.Add(btns);
            if (pins.Children.Count > 0) { var line = new Border { Height = 1 }; line.SetResourceReference(Border.BackgroundProperty, "Md.Separator"); pins.Children.Add(line); }
            pins.Children.Add(row);
        }
        content.Children.Add(Buttons(Btn("Thêm ứng dụng…", () =>
        {
            var w = new AppPickerWindow { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            w.Closed += (_, _) => Navigate("dock", true);
            w.Show();
        }, true)));

        var mag = Card(content, "Phóng to");
        Row(mag, "Phóng to khi rê chuột", Toggle(d.Magnify, v => { d.Magnify = v; T(); }));
        Row(mag, "Mức phóng to", SliderBox(1.1, 3, d.MagnifyScale, v => $"{v:0.00}×", v => { d.MagnifyScale = v; T(); }, 0.05));
        Row(mag, "Độ rộng đường cong", SliderBox(1, 6, d.CurveRange, v => $"{v:0.0} icon", v => { d.CurveRange = v; T(); }, 0.1), "Số icon lân cận bị ảnh hưởng (hàm cos)");
        Row(mag, "Tốc độ hiệu ứng", SliderBox(4, 40, d.AnimSpeed, v => $"{v:0}", v => { d.AnimSpeed = v; T(); }), "Cao = bám chuột nhanh hơn");

        var vis = Card(content, "Hiển thị");
        Row(vis, "Chấm dưới ứng dụng đang chạy", Toggle(d.ShowRunningDots, v => { d.ShowRunningDots = v; T(); }));
        Row(vis, "Nhãn tên khi rê chuột", Toggle(d.ShowLabels, v => { d.ShowLabels = v; T(); }));
        Row(vis, "Bóng đổ", Toggle(d.Shadow, v => { d.Shadow = v; T(); }));
        Row(vis, "Huy hiệu thông báo", Toggle(d.ShowBadges, v => { d.ShowBadges = v; T(); }), "Chấm đỏ khi cửa sổ cần chú ý (nhấp nháy)");
        Row(vis, "Đọc số từ tiêu đề cửa sổ", Toggle(d.BadgeFromTitle, v => { d.BadgeFromTitle = v; T(); }), "Ví dụ tiêu đề \"(3) Messenger\" → huy hiệu 3");
        Row(vis, "Hiện ứng dụng đang chạy chưa ghim", Toggle(d.ShowRunningUnpinned, v => { d.ShowRunningUnpinned = v; T(); }));
        Row(vis, "Hiện Launchpad (mở Start)", Toggle(d.ShowLaunchpad, v => { d.ShowLaunchpad = v; T(); }));
        var lpRow = new StackPanel { Orientation = Orientation.Horizontal };
        lpRow.Children.Add(Combo(new[] { ("grid", "Lưới màu (mặc định)"), ("windows", "Logo Windows 11"), ("custom", "Ảnh tự chọn") }, d.LaunchpadIcon, v => { d.LaunchpadIcon = v; T(); }));
        var lpBtn = Btn("Chọn ảnh…", () =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Ảnh/Icon (*.png;*.jpg;*.ico;*.exe)|*.png;*.jpg;*.jpeg;*.ico;*.exe" };
            if (dlg.ShowDialog(this) != true) return;
            d.LaunchpadImage = CopyToIcons(dlg.FileName); d.LaunchpadIcon = "custom"; T(); Navigate("dock", true);
        });
        lpBtn.Margin = new Thickness(8, 0, 0, 0);
        lpRow.Children.Add(lpBtn);
        Row(vis, "Biểu tượng Launchpad", lpRow);
        Row(vis, "Hiện Thùng rác", Toggle(d.ShowTrash, v => { d.ShowTrash = v; T(); }));

        var pv = Card(content, "Xem trước cửa sổ");
        Row(pv, "Xem trước khi rê chuột lên ứng dụng đang chạy", Toggle(d.WindowPreviews, v => { d.WindowPreviews = v; T(); }), "Hiện ảnh thu nhỏ trực tiếp các cửa sổ; bấm để chuyển, nút × để đóng");
        Row(pv, "Độ trễ", SliderBox(0, 1500, d.PreviewDelayMs, v => $"{v:0} ms", v => { d.PreviewDelayMs = (int)v; T(); }, 50));
        Row(pv, "Kích thước ảnh", SliderBox(120, 360, d.PreviewWidth, v => $"{v:0} px", v => { d.PreviewWidth = v; T(); }, 10));

        var anim = Card(content, "Nảy");
        Row(anim, "Nảy khi mở ứng dụng", Toggle(d.BounceOnLaunch, v => { d.BounceOnLaunch = v; T(); }));
        Row(anim, "Nảy khi ứng dụng cần chú ý", Toggle(d.BounceOnAttention, v => { d.BounceOnAttention = v; T(); }));

        var hide = Card(content, "Tự động ẩn");
        Row(hide, "Tự động ẩn và hiện Dock", Toggle(d.AutoHide, v => { d.AutoHide = v; T(); }));
        Row(hide, "Chỉ ẩn khi có cửa sổ che Dock", Toggle(d.AutoHideOnlyWhenCovered, v => { d.AutoHideOnlyWhenCovered = v; T(); }), "Ở màn hình chính (không có cửa sổ nào che) Dock luôn hiện");
        Row(hide, "Độ trễ khi hiện", SliderBox(0, 2000, d.AutoHideShowDelayMs, v => $"{v:0} ms", v => { d.AutoHideShowDelayMs = (int)v; T(); }, 50));
        Row(hide, "Độ trễ khi ẩn", SliderBox(0, 3000, d.AutoHideHideDelayMs, v => $"{v:0} ms", v => { d.AutoHideHideDelayMs = (int)v; T(); }, 50));
        Row(hide, "Chừa chỗ cho Dock", Toggle(d.ReserveSpace, v => { d.ReserveSpace = v; T(); }), "Cửa sổ phóng to không che Dock (khi không tự ẩn)");

        ResetButton(content, "dock");
        content.Children.Add(Subtle("Mẹo: kéo icon trên Dock để sắp xếp, kéo ra ngoài để gỡ; kéo file .exe/.lnk/thư mục vào Dock để ghim; kéo file lên icon ứng dụng để mở; kéo file vào Thùng rác để xoá."));
    }

    // ------------------------------------------------------------------ trang Thanh Menu

    void BuildMenuBar(StackPanel content)
    {
        var m = C.MenuBar;
        void T() => Config.Touch("menubar");
        var card = Card(content);
        Row(card, "Hiện Thanh Menu", Toggle(m.Enabled, v => { m.Enabled = v; T(); }));
        Row(card, "Chiều cao", SliderBox(20, 40, m.Height, v => $"{v:0} px", v => { m.Height = v; T(); }));
        Row(card, "Hiện trên màn hình", Combo(new[] { (MonitorMode.All, "Tất cả màn hình"), (MonitorMode.Primary, "Chỉ màn hình chính") }, m.Monitors, v => { m.Monitors = v; T(); }));
        Row(card, "Tên ứng dụng in đậm", Toggle(m.BoldAppName, v => { m.BoldAppName = v; T(); }));

        var look = Card(content, "Giao diện Thanh Menu");
        Row(look, "Kiểu nền", Combo(new[] { ("transparent", "Trong suốt (macOS 26)"), ("glass", "Kính mờ"), ("dock", "Đồng bộ với Dock"), ("solid", "Màu đặc"), ("theme", "Theo chủ đề") }, m.Style, v => { m.Style = v; T(); }));
        Row(look, "Độ đậm nền", SliderBox(0, 1.5, m.Opacity, v => $"{v * 100:0}%", v => { m.Opacity = v; T(); }, 0.05));
        Row(look, "Màu chữ", Combo(new[] { ("auto", "Tự động (theo hình nền/chủ đề)"), ("dark", "Tối"), ("light", "Sáng") }, m.TextColor, v => { m.TextColor = v; T(); }));
        var logoRow = new StackPanel { Orientation = Orientation.Horizontal };
        logoRow.Children.Add(Combo(new[] { ("circle", "Hình tròn"), ("flower", "Bông hoa"), ("custom", "Ảnh tự chọn") }, m.LogoStyle, v => { m.LogoStyle = v; T(); }));
        var logoBtn = Btn("Chọn ảnh…", () =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Ảnh (*.png;*.jpg;*.ico)|*.png;*.jpg;*.jpeg;*.ico" };
            if (dlg.ShowDialog(this) != true) return;
            m.LogoImage = CopyToIcons(dlg.FileName); m.LogoStyle = "custom"; T(); Navigate("menubar", true);
        });
        logoBtn.Margin = new Thickness(8, 0, 0, 0);
        logoRow.Children.Add(logoBtn);
        Row(look, "Biểu tượng góc trái", logoRow, "Ảnh tự chọn được cắt thành hình tròn");

        var menus = Card(content, "Menu ứng dụng");
        Row(menus, "Hiện menu của ứng dụng đang dùng", Toggle(m.ShowAppMenus, v => { m.ShowAppMenus = v; T(); }), "Menu Win32 thật nếu ứng dụng có; nếu không thì menu chung (Tệp, Sửa, Xem, Cửa sổ)");
        Row(menus, "Dùng UI Automation", Toggle(m.UseUIAutomation, v => { m.UseUIAutomation = v; T(); }), "Tìm thanh menu của ứng dụng hiện đại (chạy nền, có giới hạn thời gian)");

        var clock = Card(content, "Đồng hồ");
        var preview = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), FontWeight = FontWeights.SemiBold };
        var box = new TextBox { Text = m.ClockFormat, Width = 220, Padding = new Thickness(4, 3, 4, 3) };
        void Upd() { preview.Text = StatusService.Instance.FormatClock(box.Text, DateTime.Now); }
        box.TextChanged += (_, _) => { Upd(); if (box.Text.Trim().Length > 0) { m.ClockFormat = box.Text; T(); } };
        Upd();
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(preview); sp.Children.Add(box);
        Row(clock, "Định dạng", sp, "Chuỗi định dạng .NET: ddd = thứ, d/M = ngày/tháng, HH:mm = giờ:phút, ss = giây, tt = SA/CH");
        var presets = new WrapPanel();
        foreach (var f in new[] { "ddd d 'thg' M   HH:mm", "HH:mm", "dddd, d MMMM  HH:mm", "dd/MM/yyyy  HH:mm:ss", "ddd h:mm tt" })
        {
            var fmt = f;
            var b = Btn(StatusService.Instance.FormatClock(fmt, DateTime.Now), () => box.Text = fmt);
            b.Margin = new Thickness(0, 0, 6, 6);
            presets.Children.Add(b);
        }
        Row(clock, "Mẫu có sẵn", presets);

        var right = Card(content, "Biểu tượng bên phải");
        Row(right, "Khay hệ thống (Unikey, Zalo, OneDrive…)", Toggle(m.ShowTray, v => { m.ShowTray = v; T(); }), "Chỉ có khi đang ẩn taskbar của Windows");
        Row(right, "Bộ gõ / ngôn ngữ", Toggle(m.ShowInputLang, v => { m.ShowInputLang = v; T(); }));
        Row(right, "Pin", Toggle(m.ShowBattery, v => { m.ShowBattery = v; T(); }));
        Row(right, "Phần trăm pin", Toggle(m.ShowBatteryPercent, v => { m.ShowBatteryPercent = v; T(); }));
        Row(right, "Wi-Fi / mạng", Toggle(m.ShowWifi, v => { m.ShowWifi = v; T(); }));
        Row(right, "Âm lượng", Toggle(m.ShowVolume, v => { m.ShowVolume = v; T(); }));
        Row(right, "Trung tâm điều khiển", Toggle(m.ShowControlCenter, v => { m.ShowControlCenter = v; T(); }));
        Row(right, "Thông báo (Win+N)", Toggle(m.ShowNotifications, v => { m.ShowNotifications = v; T(); }));

        var tray = TrayService.Instance.Icons;
        if (tray.Count > 0)
        {
            var tc = Card(content, "Icon khay hệ thống đang có");
            foreach (var icon in tray.Where(i => !i.IsSystemDuplicate).OrderBy(i => i.Order))
            {
                var key = icon.PersistKey;
                var sp2 = new StackPanel { Orientation = Orientation.Horizontal };
                if (icon.Icon != null) sp2.Children.Add(new Image { Source = icon.Icon, Width = 16, Height = 16, Margin = new Thickness(0, 0, 8, 0) });
                sp2.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(icon.Tip) ? icon.ExeName : icon.Tip.Split('\n')[0], TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 380 });
                var row = new Grid { Margin = new Thickness(0, 7, 0, 7) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(sp2);
                var tg = Toggle(!m.HiddenTrayIcons.Contains(key, StringComparer.OrdinalIgnoreCase), v =>
                {
                    m.HiddenTrayIcons.RemoveAll(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
                    if (!v) m.HiddenTrayIcons.Add(key);
                    T();
                });
                Grid.SetColumn(tg, 1);
                row.Children.Add(tg);
                tc.Children.Add(row);
            }
        }
        ResetButton(content, "menubar");
    }

    // ------------------------------------------------------------------ trang Giao diện

    void BuildTheme(StackPanel content)
    {
        var s = C.Theme;
        void T() => Config.Touch("theme");
        content.Children.Add(new TextBlock { Text = "Chủ đề", FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 0, 8) });
        var wrap = new WrapPanel();
        bool dark = ThemeManager.WantDark();
        foreach (var t in ThemeManager.All()) wrap.Children.Add(ThemeCard(t, dark));
        content.Children.Add(wrap);

        var mode = Card(content, "Chế độ");
        Row(mode, "Sáng / tối", Combo(new[] { (ThemeMode.System, "Theo Windows"), (ThemeMode.Light, "Sáng"), (ThemeMode.Dark, "Tối"), (ThemeMode.Schedule, "Theo giờ") }, s.Mode, v => { s.Mode = v; T(); }));
        var times = new StackPanel { Orientation = Orientation.Horizontal };
        var lb = new TextBox { Text = s.LightStart, Width = 60, Margin = new Thickness(6, 0, 12, 0) };
        var db = new TextBox { Text = s.DarkStart, Width = 60, Margin = new Thickness(6, 0, 0, 0) };
        lb.LostFocus += (_, _) => { if (TimeSpan.TryParse(lb.Text, out _)) { s.LightStart = lb.Text; T(); } };
        db.LostFocus += (_, _) => { if (TimeSpan.TryParse(db.Text, out _)) { s.DarkStart = db.Text; T(); } };
        times.Children.Add(new TextBlock { Text = "Sáng từ", VerticalAlignment = VerticalAlignment.Center }); times.Children.Add(lb);
        times.Children.Add(new TextBlock { Text = "Tối từ", VerticalAlignment = VerticalAlignment.Center }); times.Children.Add(db);
        Row(mode, "Khung giờ (chế độ Theo giờ)", times);

        var custom = Card(content, "Tuỳ chỉnh (đè lên chủ đề)");
        var def = ThemeManager.Find(s.ThemeId);
        Row(custom, "Bo góc Dock", SliderBox(0, 32, s.CornerRadius ?? def.CornerRadius, v => $"{v:0} px", v => { s.CornerRadius = v; T(); }));
        Row(custom, "Độ đậm nền", SliderBox(0.3, 1.6, s.Opacity ?? 1, v => $"{v * 100:0}%", v => { s.Opacity = v; T(); }, 0.05), "Thấp = trong suốt hơn");
        Row(custom, "Viền", Toggle(s.Border ?? def.BorderThickness > 0, v => { s.Border = v; T(); }));
        Row(custom, "Chất liệu Thanh Menu", Combo(new[] { ("acrylic", "Kính Acrylic (làm mờ)"), ("blur", "Làm mờ nhẹ"), ("solid", "Màu đặc") }, s.MenuBarMaterial ?? def.MenuBarMaterial, v => { s.MenuBarMaterial = v; T(); }));
        var fonts = new[] { "Segoe UI Variable Text, Segoe UI", "Segoe UI", "Arial", "Tahoma", "Calibri", "Bahnschrift", "Verdana", "Consolas" }
            .Where(f => Fonts.SystemFontFamilies.Any(ff => f.Split(',')[0].Trim().Equals(ff.Source, StringComparison.OrdinalIgnoreCase))).Select(f => (f, f.Split(',')[0])).ToList();
        if (fonts.Count > 0) Row(custom, "Phông chữ", Combo(fonts, s.FontFamily ?? def.FontFamily, v => { s.FontFamily = v; T(); }));

        content.Children.Add(Buttons(
            Btn("Nhập theme (.macdocktheme)…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Theme MacDock (*.macdocktheme;*.json)|*.macdocktheme;*.json" };
                if (dlg.ShowDialog(this) != true) return;
                var id = ThemeManager.Import(dlg.FileName);
                s.ThemeId = id; T();
                Navigate("theme", true);
            }),
            Btn("Xuất theme hiện tại…", () =>
            {
                var eff = ThemeManager.EffectiveDefinition();
                var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Theme MacDock (*.macdocktheme)|*.macdocktheme", FileName = eff.Name + ".macdocktheme" };
                if (dlg.ShowDialog(this) != true) return;
                ThemeManager.Export(eff, dlg.FileName);
                MessageBox.Show("Đã xuất theme ra:\n" + dlg.FileName, "MacDock");
            }),
            Btn("Lưu thành theme mới…", () =>
            {
                var name = Prompt("Tên theme mới:", ThemeManager.Find(s.ThemeId).Name + " (của tôi)");
                if (string.IsNullOrWhiteSpace(name)) return;
                var id = ThemeManager.SaveAsUserTheme(ThemeManager.EffectiveDefinition(), name);
                s.ThemeId = id; s.CornerRadius = null; s.Opacity = null; s.Border = null; s.FontFamily = null; s.MenuBarMaterial = null;
                T(); Navigate("theme", true);
            }),
            Btn("Mở thư mục theme", () => Launcher.Open(Paths.ThemesDir)),
            Btn("Bỏ tuỳ chỉnh", () => { s.CornerRadius = null; s.Opacity = null; s.Border = null; s.FontFamily = null; s.MenuBarMaterial = null; T(); Navigate("theme", true); })));
        content.Children.Add(Subtle("Theme là file theme.json (màu sáng/tối, độ trong suốt, gradient, viền, bo góc, bóng, phông chữ, chất liệu). Gói .macdocktheme là file zip chứa theme.json và thư mục icons/ (icon pack)."));
    }

    Border ThemeCard(ThemeDefinition t, bool dark)
    {
        var p = dark ? t.Dark : t.Light;
        Color Cc(string? s, string fb) { try { return (Color)ColorConverter.ConvertFromString(string.IsNullOrEmpty(s) ? fb : s); } catch { return Colors.Gray; } }
        var canvas = new Canvas { Width = 150, Height = 92, ClipToBounds = true };
        var wall = new LinearGradientBrush(dark ? Color.FromRgb(0x2B, 0x32, 0x5C) : Color.FromRgb(0x8E, 0xC5, 0xFF), dark ? Color.FromRgb(0x5B, 0x2A, 0x4E) : Color.FromRgb(0xFF, 0xC3, 0xA0), 45);
        canvas.Children.Add(new WShapes.Rectangle { Width = 150, Height = 92, Fill = wall });
        canvas.Children.Add(new WShapes.Rectangle { Width = 150, Height = 10, Fill = new SolidColorBrush(Cc(p.MenuBarBackground, "#B3FFFFFF")) });
        var plate = new WShapes.Rectangle { Width = 104, Height = 22, RadiusX = Math.Min(t.CornerRadius / 2.2, 11), RadiusY = Math.Min(t.CornerRadius / 2.2, 11), Fill = new SolidColorBrush(Cc(p.DockBackground, "#66FFFFFF")), Stroke = t.BorderThickness > 0 ? new SolidColorBrush(Cc(p.DockBorder, "#40FFFFFF")) : null, StrokeThickness = 0.8 };
        Canvas.SetLeft(plate, 23); Canvas.SetTop(plate, 64);
        canvas.Children.Add(plate);
        var colors = new[] { "#FF0A84FF", "#FF30D158", "#FFFF9F0A", "#FFFF375F", "#FFBF5AF2" };
        for (int i = 0; i < 5; i++)
        {
            var r = new WShapes.Rectangle { Width = 15, Height = 15, RadiusX = 4, RadiusY = 4, Fill = new SolidColorBrush(Cc(colors[i], "#FF888888")) };
            Canvas.SetLeft(r, 29 + i * 19.5); Canvas.SetTop(r, 67.5);
            canvas.Children.Add(r);
        }
        var popup = new WShapes.Rectangle { Width = 46, Height = 30, RadiusX = 4, RadiusY = 4, Fill = new SolidColorBrush(Cc(p.PopupBackground, "#F2FFFFFF")) };
        Canvas.SetLeft(popup, 8); Canvas.SetTop(popup, 14);
        canvas.Children.Add(popup);
        var sel = new WShapes.Rectangle { Width = 40, Height = 6, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(Cc(p.Selection, "#FF0A64D8")) };
        Canvas.SetLeft(sel, 11); Canvas.SetTop(sel, 22);
        canvas.Children.Add(sel);

        bool current = C.Theme.ThemeId.Equals(t.Id, StringComparison.OrdinalIgnoreCase);
        var stack = new StackPanel();
        stack.Children.Add(new Border { Child = canvas, CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderThickness = new Thickness(current ? 3 : 1), BorderBrush = current ? ThemeManager.Current.Accent : ThemeManager.Current.Separator });
        var name = new TextBlock { Text = t.Name, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0), FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal };
        stack.Children.Add(name);
        var b = new Border { Child = stack, Margin = new Thickness(0, 0, 14, 14), Cursor = Cursors.Hand, Background = Brushes.Transparent };
        b.MouseLeftButtonUp += (_, _) =>
        {
            C.Theme.ThemeId = t.Id;
            Config.Touch("theme");
            Dispatcher.BeginInvoke(() => Navigate("theme", true), DispatcherPriority.Background);
        };
        if (!t.BuiltIn && t.Folder != null)
        {
            var cm = new ContextMenu();
            cm.Items.Add(DockModel.Mi("Xoá theme này", () =>
            {
                if (MessageBox.Show($"Xoá theme \"{t.Name}\"?", "MacDock", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                ThemeManager.DeleteUserTheme(t.Id);
                if (current) { C.Theme.ThemeId = "sonoma"; Config.Touch("theme"); }
                Navigate("theme", true);
            }));
            b.ContextMenu = cm;
        }
        return b;
    }

    string? Prompt(string label, string value)
    {
        var w = new Window { Title = "MacDock", Width = 380, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize };
        w.SetResourceReference(BackgroundProperty, "Md.WindowBg");
        var sp = new StackPanel { Margin = new Thickness(16) };
        sp.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6) });
        var tb = new TextBox { Text = value, Padding = new Thickness(4) };
        sp.Children.Add(tb);
        string? result = null;
        var ok = Btn("OK", () => { result = tb.Text; w.Close(); }, true);
        ok.IsDefault = true;
        var buttons = Buttons(ok, Btn("Huỷ", () => w.Close()));
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        sp.Children.Add(buttons);
        w.Content = sp;
        tb.SelectAll(); tb.Focus();
        w.ShowDialog();
        return result;
    }

    // ------------------------------------------------------------------ trang Biểu tượng

    void BuildIcons(StackPanel content)
    {
        var s = C.Theme;
        void T() => Config.Touch("theme");
        var card = Card(content);
        Row(card, "Kiểu icon", Combo(new[] { (IconStyle.Original, "Gốc của ứng dụng"), (IconStyle.Squircle, "Ô bo tròn (squircle)") }, s.IconStyle, v => { s.IconStyle = v; T(); }));
        var packs = new List<(string, string)> { ("", "(không dùng)") };
        packs.AddRange(ThemeManager.IconPacks().Select(p => (p.folder, p.name)));
        if (s.IconPack != null && !packs.Any(p => p.Item1 == s.IconPack)) packs.Add((s.IconPack, Path.GetFileName(s.IconPack)));
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(Combo(packs, s.IconPack ?? "", v => { s.IconPack = string.IsNullOrEmpty(v) ? null : v; T(); }));
        Row(card, "Icon pack", sp, "Thư mục chứa ảnh .png/.ico đặt tên theo exe (vd: chrome.png) hoặc AUMID. Đặt trong " + Paths.IconsDir);
        Row(card, "Thư mục icon", Btn("Mở", () => Launcher.Open(Paths.IconsDir)));

        var apps = Card(content, "Đổi icon từng ứng dụng");
        foreach (var item in DockModel.Instance.Items.Where(i => i.Kind is DockItemKind.App or DockItemKind.Launchpad))
        {
            var it = item;
            var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { Orientation = Orientation.Horizontal };
            var img = DockModel.Instance.GetIcon(it);
            left.Children.Add(new Image { Source = img, Width = 26, Height = 26, Margin = new Thickness(0, 0, 10, 0) });
            left.Children.Add(new TextBlock { Text = it.Name, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(left);
            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            btns.Children.Add(Btn("Đổi…", () => { DockModel.Instance.ChangeIcon(it); Navigate("icons", true); }));
            if (s.IconOverrides.ContainsKey(it.Key)) btns.Children.Add(Btn("Mặc định", () => { DockModel.Instance.ResetIcon(it); Navigate("icons", true); }));
            Grid.SetColumn(btns, 1);
            row.Children.Add(btns);
            if (apps.Children.Count > 0) { var line = new Border { Height = 1 }; line.SetResourceReference(Border.BackgroundProperty, "Md.Separator"); apps.Children.Add(line); }
            apps.Children.Add(row);
        }
    }

    // ------------------------------------------------------------------ trang Hiệu năng

    void BuildPerformance(StackPanel content)
    {
        var p = C.Performance;
        void T() => Config.Touch("performance");
        var card = Card(content);
        Row(card, "Giới hạn khung hình", Combo(new[] { (30, "30 FPS"), (60, "60 FPS"), (120, "120 FPS"), (0, "Không giới hạn (theo màn hình)") }, p.FpsLimit, v => { p.FpsLimit = v; T(); }), "Chỉ ảnh hưởng lúc đang có hiệu ứng; khi rảnh MacDock không vẽ lại");
        Row(card, "Tắt hiệu ứng", Toggle(p.DisableAnimations, v => { p.DisableAnimations = v; T(); }), "Không phóng to, không nảy, chuyển cảnh tức thì");
        Row(card, "Giảm hiệu ứng khi dùng pin", Toggle(p.ReduceOnBattery, v => { p.ReduceOnBattery = v; T(); }), "Tối đa 30 FPS và không nảy khi rút sạc");
        Row(card, "Thu gọn bộ nhớ khi rảnh", Toggle(p.TrimMemory, v => { p.TrimMemory = v; T(); }), "Trả lại RAM cho hệ thống sau khi khởi động / đóng Cài đặt");

        var live = Card(content, "Đang dùng");
        var ram = new TextBlock { FontWeight = FontWeights.SemiBold };
        var cpu = new TextBlock { FontWeight = FontWeights.SemiBold };
        Row(live, "Bộ nhớ (working set / private)", ram);
        Row(live, "CPU (trung bình 2 giây)", cpu);
        Row(live, "Trạng thái nguồn", new TextBlock { Text = StatusService.Instance.OnBattery ? "Đang dùng pin" : "Đang cắm sạc / không có pin" });
        var proc = Process.GetCurrentProcess();
        var lastCpu = proc.TotalProcessorTime; var lastT = DateTime.Now;
        void Upd()
        {
            proc.Refresh();
            ram.Text = $"{proc.WorkingSet64 / 1048576.0:0.0} MB / {proc.PrivateMemorySize64 / 1048576.0:0.0} MB";
            var now = DateTime.Now; var c = proc.TotalProcessorTime;
            double pct = (c - lastCpu).TotalMilliseconds / Math.Max(1, (now - lastT).TotalMilliseconds) / Environment.ProcessorCount * 100;
            cpu.Text = $"{pct:0.0}%";
            lastCpu = c; lastT = now;
        }
        Upd();
        _perfTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _perfTimer.Tick += (_, _) => Upd();
        _perfTimer.Start();
        content.Children.Add(Buttons(Btn("Giải phóng bộ nhớ ngay", () => { App.TrimMemory(); Upd(); })));
        ResetButton(content, "performance");
    }

    // ------------------------------------------------------------------ trang Giới thiệu

    void BuildAbout(StackPanel content)
    {
        var card = Card(content);
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 14) };
        sp.Children.Add(new Image { Source = Glyphs.AppIcon, Width = 72, Height = 72 });
        var info = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 560 };
        info.Children.Add(new TextBlock { Text = "MacDock", FontSize = 20, FontWeight = FontWeights.SemiBold });
        info.Children.Add(new TextBlock { Text = "Phiên bản " + App.VersionString });
        info.Children.Add(Subtle("Bộ giao diện kiểu macOS cho Windows 11: Dock, Thanh Menu, theme. Phần mềm gốc, không dùng tài nguyên của Apple."));
        info.Children.Add(Subtle("Mã nguồn mở — giấy phép MIT."));
        sp.Children.Add(info);
        card.Children.Add(sp);
        var dev = Card(content, "Nhà phát triển");
        dev.Children.Add(Developer.Build());

        var c2 = Card(content, "Thông tin");
        Row(c2, "Mã nguồn", Btn("Mở GitHub", () => Launcher.Open(Developer.Repo)));
        Row(c2, "Thư mục dữ liệu", new TextBlock { Text = Paths.Root, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 });
        Row(c2, "File chạy", new TextBlock { Text = Paths.ExePath, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 });
        Row(c2, "Nhật ký", Btn("Mở log", () => Launcher.Open(Paths.LogFile)));
        Row(c2, "Khôi phục taskbar bằng dòng lệnh", new TextBox { Text = "MacDock.exe --restore-taskbar", IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent });
    }
}
