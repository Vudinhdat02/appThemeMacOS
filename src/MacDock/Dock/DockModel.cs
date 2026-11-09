using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MacDock;

public enum DockItemKind { Launchpad, App, Separator, Trash }

public sealed class DockItem
{
    public DockItemKind Kind { get; init; }
    public string Key { get; init; } = "";
    public string Name { get; set; } = "";
    public string? ExePath { get; set; }
    public string? Aumid { get; set; }
    public string PinKind { get; set; } = "exe";
    public string? Args { get; set; }
    public bool Pinned { get; set; }
    public List<AppWindow> Windows { get; set; } = new();
    public bool Running => Windows.Count > 0;
    public ImageSource? Icon { get; set; }
    public bool IconRequested { get; set; }
    public int Badge { get; set; } // >0 số, -1 chấm
    public bool TrashFull { get; set; }

    // Nảy
    public double BounceStart { get; set; } = -1;
    public int BounceCount { get; set; }
    public double BounceHeight { get; set; }
    public double BouncePeriod { get; set; } = 0.6;

    public bool IsDraggable => Kind == DockItemKind.App;
    public override string ToString() => $"{Kind}:{Name}";
}

/// <summary>Danh sách mục trên Dock (dùng chung cho mọi màn hình) và các hành động.</summary>
public sealed class DockModel
{
    public static DockModel Instance { get; } = new();

    public List<DockItem> Items { get; private set; } = new();
    readonly Dictionary<string, DockItem> _byKey = new(StringComparer.OrdinalIgnoreCase);
    readonly List<string> _runningOrder = new();
    readonly DockItem _launchpad = new() { Kind = DockItemKind.Launchpad, Key = "special:launchpad", Name = "Launchpad (Start)" };
    readonly DockItem _trash = new() { Kind = DockItemKind.Trash, Key = "special:trash", Name = "Thùng rác" };
    readonly DockItem _sep1 = new() { Kind = DockItemKind.Separator, Key = "special:sep1" };
    readonly DockItem _sep2 = new() { Kind = DockItemKind.Separator, Key = "special:sep2" };
    DispatcherTimer? _trashTimer;
    string _lastSig = "";

    /// <summary>Danh sách thay đổi cấu trúc (thêm/bớt mục).</summary>
    public event Action? ItemsChanged;
    /// <summary>Chỉ cần vẽ lại (icon nạp xong, chấm, huy hiệu, nảy).</summary>
    public event Action? Invalidated;

    public static readonly Stopwatch Clock = Stopwatch.StartNew();
    public static double Now => Clock.Elapsed.TotalSeconds;

    public void Start()
    {
        WindowTracker.Instance.Changed += Rebuild;
        WindowTracker.Instance.Attention += OnAttention;
        Config.Changed += s => { if (s is "dock" or "pins" or "all" or "theme") Rebuild(); };
        ThemeManager.Changed += () => { foreach (var i in _byKey.Values) { i.Icon = null; i.IconRequested = false; } Invalidated?.Invoke(); };
        _trashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _trashTimer.Tick += (_, _) => RefreshTrash();
        _trashTimer.Start();
        RefreshTrash();
        Rebuild();
    }

    void RefreshTrash()
    {
        try
        {
            var info = new SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<SHQUERYRBINFO>() };
            bool full = Native.SHQueryRecycleBin(null, ref info) == 0 && info.i64NumItems > 0;
            if (full != _trash.TrashFull) { _trash.TrashFull = full; Invalidated?.Invoke(); }
        }
        catch { }
    }

    DockItem GetOrCreate(string key, Func<DockItem> create)
    {
        if (!_byKey.TryGetValue(key, out var item)) { item = create(); _byKey[key] = item; }
        return item;
    }

    public void Rebuild()
    {
        var cfg = Config.Current.Dock;
        var tracker = WindowTracker.Instance;
        var groups = tracker.Windows.GroupBy(w => MapToPin(w.Id, cfg.Pinned)).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var list = new List<DockItem>();
        if (cfg.ShowLaunchpad) list.Add(_launchpad);

        var pinnedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in cfg.Pinned)
        {
            if (!pinnedKeys.Add(p.Key)) continue;
            var item = GetOrCreate(p.Key, () => new DockItem { Kind = DockItemKind.App, Key = p.Key });
            item.Pinned = true;
            item.PinKind = p.Kind;
            item.Args = p.Args;
            if (p.Kind == "aumid") { item.Aumid = p.Path; item.ExePath = null; }
            else item.ExePath = p.Path;
            if (string.IsNullOrEmpty(item.Name) || p.Name != null)
                item.Name = p.Name ?? AppIdentity.DisplayName(p.Key, item.ExePath, item.Aumid);
            list.Add(item);
        }

        // thứ tự app đang chạy chưa ghim
        foreach (var k in groups.Keys) if (!_runningOrder.Contains(k)) _runningOrder.Add(k);
        _runningOrder.RemoveAll(k => !groups.ContainsKey(k));

        var running = new List<DockItem>();
        if (cfg.ShowRunningUnpinned)
        {
            foreach (var k in _runningOrder)
            {
                if (pinnedKeys.Contains(k)) continue;
                var w0 = groups[k][0];
                var item = GetOrCreate(k, () => new DockItem { Kind = DockItemKind.App, Key = k });
                item.Pinned = false;
                item.PinKind = w0.Id.Aumid != null ? "aumid" : "exe";
                item.Aumid = w0.Id.Aumid;
                item.ExePath = w0.Id.ExePath;
                if (string.IsNullOrEmpty(item.Name)) item.Name = AppIdentity.DisplayName(k, item.ExePath, item.Aumid, w0.Title);
                running.Add(item);
            }
        }
        if (running.Count > 0 && list.Any(i => i.Kind == DockItemKind.App)) list.Add(_sep1);
        list.AddRange(running);

        if (cfg.ShowTrash) { list.Add(_sep2); list.Add(_trash); }

        // gán cửa sổ + huy hiệu
        foreach (var item in list)
        {
            if (item.Kind != DockItemKind.App) continue;
            item.Windows = groups.TryGetValue(item.Key, out var ws) ? ws : new List<AppWindow>();
            if (item.Running && item.BounceStart >= 0 && item.BounceHeight < Config.Current.Dock.IconSize * 0.8)
            {
                // app đã mở: cho lần nảy hiện tại kết thúc rồi dừng
                item.BounceCount = Math.Min(item.BounceCount, (int)Math.Ceiling((Now - item.BounceStart) / item.BouncePeriod));
            }
            int badge = 0;
            if (cfg.ShowBadges)
            {
                if (cfg.BadgeFromTitle) badge = item.Windows.Select(w => w.TitleBadge).DefaultIfEmpty(0).Max();
                if (badge == 0 && item.Windows.Any(w => w.Flashing)) badge = -1;
            }
            item.Badge = badge;
        }

        bool structural = list.Count != Items.Count || !list.SequenceEqual(Items);
        Items = list;
        if (structural) ItemsChanged?.Invoke();
        // chỉ vẽ lại khi trạng thái hiển thị thật sự thay đổi
        var sig = string.Join("|", list.Select(i => $"{i.Key}:{i.Windows.Count}:{i.Badge}:{i.Name}"));
        if (structural || sig != _lastSig) { if (Log.Verbose) Diag.Hit(structural ? "structural" : "sig"); _lastSig = sig; Invalidated?.Invoke(); }
    }

    /// <summary>App chạy từ thư mục con theo phiên bản (vd Zalo\Zalo-25.x\Zalo.exe) vẫn khớp với mục ghim Zalo\Zalo.exe.</summary>
    static string MapToPin(AppId id, List<PinnedItem> pins)
    {
        if (id.Aumid != null || id.ExePath == null) return id.Key;
        string file = Path.GetFileName(id.ExePath);
        foreach (var p in pins)
        {
            if (p.Kind != "exe") continue;
            if (p.Key == id.Key) return id.Key;
            var dir = Path.GetDirectoryName(p.Path);
            if (dir != null && Path.GetFileName(p.Path).Equals(file, StringComparison.OrdinalIgnoreCase)
                && id.ExePath.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase)) return p.Key;
        }
        return id.Key;
    }

    // ---------------- Icon ----------------

    public ImageSource? GetIcon(DockItem item)
    {
        switch (item.Kind)
        {
            case DockItemKind.Launchpad:
                return OverrideIcon(item) ?? Glyphs.Launchpad;
            case DockItemKind.Trash:
                return Glyphs.Trash(item.TrashFull);
            case DockItemKind.Separator:
                return null;
        }
        if (item.Icon != null) return item.Icon;
        if (item.IconRequested) return null;
        item.IconRequested = true;
        var ov = OverrideIcon(item);
        if (ov != null) { item.Icon = ov; return ov; }
        if (item.ExePath != null && item.ExePath.Equals(Paths.ExePath, StringComparison.OrdinalIgnoreCase))
        {
            item.Icon = Glyphs.AppIcon; return item.Icon;
        }
        string parsing = AppIdentity.IconParsingName(item.Key, item.ExePath, item.Aumid);
        var img = IconCache.Get(parsing, loaded => { item.Icon = loaded; Invalidated?.Invoke(); });
        if (img != null) item.Icon = img;
        return img;
    }

    static ImageSource? OverrideIcon(DockItem item)
    {
        var f = ThemeManager.FindIconOverride(item.Key, item.ExePath, item.Aumid);
        return f != null ? ShellIcons.LoadImageFile(f, IconCache.IconPixelSize) : null;
    }

    // ---------------- Nảy ----------------

    public void Bounce(DockItem item, int count, double heightFactor, double period = 0.6)
    {
        if (Config.Current.Performance.DisableAnimations) return;
        item.BounceStart = Now;
        item.BounceCount = count;
        item.BounceHeight = Config.Current.Dock.IconSize * heightFactor;
        item.BouncePeriod = period;
        Invalidated?.Invoke();
    }

    /// <summary>Độ cao nảy hiện tại (DIP) — 0 nếu không nảy. Tự xoá trạng thái khi xong.</summary>
    public static double BounceOffset(DockItem item, double now, out bool active)
    {
        active = false;
        if (item.BounceStart < 0) return 0;
        double t = now - item.BounceStart;
        if (t >= item.BounceCount * item.BouncePeriod) { item.BounceStart = -1; return 0; }
        active = true;
        double ph = (t % item.BouncePeriod) / item.BouncePeriod;
        return item.BounceHeight * 4 * ph * (1 - ph);
    }

    void OnAttention(AppWindow w)
    {
        if (!Config.Current.Dock.BounceOnAttention) return;
        if (_byKey.TryGetValue(w.Id.Key, out var item) && Items.Contains(item)) Bounce(item, 2, 0.9, 0.7);
    }

    // ---------------- Hành động ----------------

    public void Activate(DockItem item)
    {
        switch (item.Kind)
        {
            case DockItemKind.Launchpad:
                Native.SendKeys(Native.VK_LWIN);
                return;
            case DockItemKind.Trash:
                Launcher.Open("shell:RecycleBinFolder");
                return;
            case DockItemKind.Separator:
                return;
        }
        if (item.PinKind == "folder") { Launcher.Launch("folder", item.Key.Substring(7)); return; }

        var windows = item.Windows.Where(w => Native.IsWindow(w.Hwnd)).ToList();
        if (windows.Count == 0)
        {
            if (Launch(item) && Config.Current.Dock.BounceOnLaunch) Bounce(item, 3, 0.55);
            return;
        }
        IntPtr fg = Native.GetForegroundWindow();
        IntPtr fgRoot = Native.GetAncestor(fg, Native.GA_ROOTOWNER);
        var stable = windows.OrderBy(w => w.Hwnd.ToInt64()).ToList();
        int idx = stable.FindIndex(w => w.Hwnd == fg || w.Hwnd == fgRoot);
        if (idx >= 0 && !stable[idx].Minimized)
        {
            if (stable.Count == 1) WindowActions.Minimize(stable[0].Hwnd);
            else WindowActions.Activate(stable[(idx + 1) % stable.Count].Hwnd);
        }
        else
        {
            var target = windows.OrderByDescending(w => w.LastActive).First();
            WindowActions.Activate(target.Hwnd);
        }
    }

    public bool Launch(DockItem item, string? file = null)
    {
        if (item.Aumid != null) return Launcher.Launch("aumid", item.Aumid);
        if (item.ExePath != null) return Launcher.Launch(item.PinKind == "folder" ? "folder" : "exe", item.ExePath, item.Args, file);
        return false;
    }

    public void SetPinned(DockItem item, bool pin, int index = -1)
    {
        var pins = Config.Current.Dock.Pinned;
        pins.RemoveAll(p => p.Key.Equals(item.Key, StringComparison.OrdinalIgnoreCase));
        if (pin)
        {
            var p = new PinnedItem { Kind = item.Aumid != null ? "aumid" : item.PinKind, Path = item.Aumid ?? item.ExePath ?? "", Args = item.Args };
            if (p.Kind == "folder") p.Path = item.Key.Substring(7);
            if (index < 0 || index > pins.Count) pins.Add(p); else pins.Insert(index, p);
        }
        Config.Touch("pins");
    }

    /// <summary>Sắp xếp lại sau khi kéo thả: <paramref name="appOrder"/> là thứ tự mới của các mục App.</summary>
    public void Reorder(IList<DockItem> appOrder, DockItem dragged)
    {
        var pins = Config.Current.Dock.Pinned;
        var byKey = pins.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
        var newPins = new List<PinnedItem>();
        foreach (var it in appOrder)
        {
            if (!(it.Pinned || it == dragged)) continue;
            if (byKey.TryGetValue(it.Key, out var p)) newPins.Add(p);
            else newPins.Add(new PinnedItem { Kind = it.Aumid != null ? "aumid" : it.PinKind, Path = it.Aumid ?? it.ExePath ?? "", Args = it.Args });
        }
        Config.Current.Dock.Pinned = newPins;
        Config.Touch("pins");
    }

    public void Move(DockItem item, int delta)
    {
        var pins = Config.Current.Dock.Pinned;
        int i = pins.FindIndex(p => p.Key.Equals(item.Key, StringComparison.OrdinalIgnoreCase));
        if (i < 0) return;
        int j = Math.Clamp(i + delta, 0, pins.Count - 1);
        if (i == j) return;
        var p = pins[i]; pins.RemoveAt(i); pins.Insert(j, p);
        Config.Touch("pins");
    }

    public void QuitApp(DockItem item, bool force)
    {
        foreach (var w in item.Windows.ToList())
        {
            if (force) WindowActions.KillProcess(w.Id.Pid);
            else WindowActions.Close(w.Hwnd);
        }
    }

    /// <summary>Thả file từ Explorer lên Dock.</summary>
    public void HandleDrop(string[] files, DockItem? target, int insertIndex)
    {
        if (target?.Kind == DockItemKind.Trash)
        {
            Recycle(files);
            return;
        }
        if (target?.Kind == DockItemKind.App && !(files.Length == 1 && IsPinnable(files[0]) && target.Key.Equals(files[0], StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var f in files) Launch(target, f);
            if (!target.Running && Config.Current.Dock.BounceOnLaunch) Bounce(target, 3, 0.55);
            return;
        }
        // ghim exe / lnk / thư mục
        var pins = Config.Current.Dock.Pinned;
        int pinIndex = PinIndexForInsert(insertIndex);
        foreach (var f in files)
        {
            PinnedItem? p = null;
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (Directory.Exists(f)) p = new PinnedItem { Kind = "folder", Path = f };
            else if (ext == ".exe") p = new PinnedItem { Kind = "exe", Path = f };
            else if (ext == ".lnk")
            {
                var t = AppIdentity.ResolveShortcut(f);
                if (t != null && t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(t)) p = new PinnedItem { Kind = "exe", Path = t };
                else if (t != null && Directory.Exists(t)) p = new PinnedItem { Kind = "folder", Path = t };
            }
            if (p == null) continue;
            if (pins.Any(x => x.Key == p.Key)) continue;
            pins.Insert(Math.Clamp(pinIndex++, 0, pins.Count), p);
        }
        Config.Touch("pins");
    }

    int PinIndexForInsert(int insertIndex)
    {
        // insertIndex là vị trí trong Items; đổi ra vị trí trong danh sách ghim
        int count = 0;
        for (int i = 0; i < Math.Min(insertIndex, Items.Count); i++) if (Items[i].Pinned) count++;
        return count;
    }

    public static bool IsPinnable(string f)
    {
        var ext = Path.GetExtension(f).ToLowerInvariant();
        return ext is ".exe" or ".lnk" || Directory.Exists(f);
    }

    public void Recycle(string[] files)
    {
        try
        {
            var op = new SHFILEOPSTRUCT
            {
                wFunc = Native.FO_DELETE,
                pFrom = string.Join("\0", files) + "\0\0",
                fFlags = (ushort)(Native.FOF_ALLOWUNDO | Native.FOF_WANTNUKEWARNING)
            };
            Native.SHFileOperation(ref op);
        }
        catch (Exception ex) { Log.Warn("Xoá vào Thùng rác lỗi: " + ex.Message); }
        RefreshTrash();
    }

    public void EmptyTrash()
    {
        // Hộp thoại xác nhận của Windows sẽ hiện ra (không dùng cờ bỏ xác nhận)
        Native.SHEmptyRecycleBin(IntPtr.Zero, null, 0);
        RefreshTrash();
    }

    public void ChangeIcon(DockItem item)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Ảnh/Icon (*.png;*.ico;*.exe)|*.png;*.ico;*.exe|Tất cả|*.*", Title = "Chọn biểu tượng cho " + item.Name };
        if (dlg.ShowDialog() == true)
        {
            string dest = Path.Combine(Paths.IconsDir, "custom");
            Directory.CreateDirectory(dest);
            string file = Path.Combine(dest, Guid.NewGuid().ToString("N").Substring(0, 8) + Path.GetExtension(dlg.FileName));
            try { File.Copy(dlg.FileName, file, true); } catch { file = dlg.FileName; }
            Config.Current.Theme.IconOverrides[item.Key] = file;
            item.Icon = null; item.IconRequested = false;
            Config.Touch("theme");
        }
    }

    public void ResetIcon(DockItem item)
    {
        Config.Current.Theme.IconOverrides.Remove(item.Key);
        item.Icon = null; item.IconRequested = false;
        Config.Touch("theme");
    }

    // ---------------- Menu chuột phải ----------------

    public ContextMenu BuildMenu(DockItem item)
    {
        var m = new ContextMenu();
        switch (item.Kind)
        {
            case DockItemKind.Trash:
                m.Items.Add(Mi("Mở", () => Launcher.Open("shell:RecycleBinFolder")));
                m.Items.Add(Mi("Dọn sạch Thùng rác…", EmptyTrash, item.TrashFull));
                m.Items.Add(new Separator());
                AddDockOptions(m);
                return m;
            case DockItemKind.Launchpad:
            case DockItemKind.Separator:
                AddDockOptions(m);
                return m;
        }

        var cfgPinned = item.Pinned;
        IntPtr fg = Native.GetForegroundWindow();
        foreach (var w in item.Windows.OrderBy(w => w.Hwnd.ToInt64()))
        {
            string title = w.Title.Length > 60 ? w.Title.Substring(0, 57) + "…" : w.Title;
            var mi = Mi(title + (w.Minimized ? "  (thu nhỏ)" : ""), () => WindowActions.Activate(w.Hwnd));
            mi.IsChecked = w.Hwnd == fg;
            m.Items.Add(mi);
        }
        if (item.Windows.Count > 0) m.Items.Add(new Separator());

        if (item.PinKind != "folder")
            m.Items.Add(Mi(item.Running ? "Mở cửa sổ mới" : "Mở", () => { if (Launch(item) && !item.Running && Config.Current.Dock.BounceOnLaunch) Bounce(item, 3, 0.55); }));
        else m.Items.Add(Mi("Mở thư mục", () => Launcher.Launch("folder", item.Key.Substring(7))));

        var opt = new MenuItem { Header = "Tuỳ chọn" };
        var keep = Mi("Giữ trên Dock", () => SetPinned(item, !cfgPinned));
        keep.IsChecked = cfgPinned;
        opt.Items.Add(keep);
        if (cfgPinned)
        {
            opt.Items.Add(Mi("Chuyển sang trái", () => Move(item, -1)));
            opt.Items.Add(Mi("Chuyển sang phải", () => Move(item, +1)));
        }
        string? showPath = item.PinKind == "folder" ? item.Key.Substring(7) : item.ExePath;
        if (showPath != null) opt.Items.Add(Mi("Hiện trong Explorer", () => Launcher.ShowInExplorer(showPath)));
        opt.Items.Add(new Separator());
        opt.Items.Add(Mi("Đổi biểu tượng…", () => ChangeIcon(item)));
        if (Config.Current.Theme.IconOverrides.ContainsKey(item.Key)) opt.Items.Add(Mi("Dùng biểu tượng gốc", () => ResetIcon(item)));
        m.Items.Add(opt);

        if (item.Running)
        {
            m.Items.Add(new Separator());
            m.Items.Add(Mi("Ẩn", () => { foreach (var w in item.Windows) WindowActions.Minimize(w.Hwnd); }));
            m.Items.Add(Mi("Thoát", () => QuitApp(item, false)));
            m.Items.Add(Mi("Buộc thoát", () =>
            {
                if (MessageBox.Show($"Buộc thoát {item.Name}? Dữ liệu chưa lưu sẽ bị mất.", "MacDock", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    QuitApp(item, true);
            }));
        }
        return m;
    }

    void AddDockOptions(ContextMenu m)
    {
        var cfg = Config.Current.Dock;
        var ah = Mi("Tự động ẩn Dock", () => { cfg.AutoHide = !cfg.AutoHide; Config.Touch("dock"); });
        ah.IsChecked = cfg.AutoHide;
        m.Items.Add(ah);
        var mag = Mi("Phóng to khi rê chuột", () => { cfg.Magnify = !cfg.Magnify; Config.Touch("dock"); });
        mag.IsChecked = cfg.Magnify;
        m.Items.Add(mag);

        var pos = new MenuItem { Header = "Vị trí trên màn hình" };
        foreach (var (edge, label) in new[] { (DockEdge.Left, "Trái"), (DockEdge.Bottom, "Dưới"), (DockEdge.Right, "Phải") })
        {
            var e = edge;
            var mi = Mi(label, () => { cfg.Edge = e; Config.Touch("dock"); });
            mi.IsChecked = cfg.Edge == e;
            pos.Items.Add(mi);
        }
        m.Items.Add(pos);

        var theme = new MenuItem { Header = "Giao diện" };
        foreach (var t in ThemeManager.All())
        {
            var id = t.Id;
            var mi = Mi(t.Name, () => { Config.Current.Theme.ThemeId = id; Config.Touch("theme"); });
            mi.IsChecked = Config.Current.Theme.ThemeId.Equals(id, StringComparison.OrdinalIgnoreCase);
            theme.Items.Add(mi);
        }
        theme.Items.Add(new Separator());
        foreach (var (mode, label) in new[] { (ThemeMode.Light, "Sáng"), (ThemeMode.Dark, "Tối"), (ThemeMode.System, "Theo Windows"), (ThemeMode.Schedule, "Theo giờ") })
        {
            var md = mode;
            var mi = Mi(label, () => { Config.Current.Theme.Mode = md; Config.Touch("theme"); });
            mi.IsChecked = Config.Current.Theme.Mode == md;
            theme.Items.Add(mi);
        }
        m.Items.Add(theme);
        m.Items.Add(new Separator());
        m.Items.Add(Mi("Cài đặt Dock…", () => App.Instance.ShowSettings("dock")));
        m.Items.Add(Mi("Thoát MacDock", () => App.Instance.Quit()));
    }

    public static MenuItem Mi(string header, Action action, bool enabled = true)
    {
        var mi = new MenuItem { Header = header.Replace("_", "__"), IsEnabled = enabled };
        mi.Click += (_, _) =>
        {
            try { action(); } catch (Exception ex) { Log.Error("Lệnh menu lỗi", ex); }
        };
        return mi;
    }
}
