using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace MacDock;

public sealed class TopMenu
{
    public string Title { get; init; } = "";
    /// <summary>Tạo menu thả xuống (menu Win32 hoặc menu chung).</summary>
    public Func<ContextMenu>? Build { get; init; }
    /// <summary>Hoặc: kích hoạt menu thật của app qua UI Automation.</summary>
    public Action? Invoke { get; init; }
}

/// <summary>Đọc menu thật của ứng dụng: menu Win32 (GetMenu) hoặc UI Automation; nếu không có thì dùng menu chung.</summary>
public static class AppMenuProvider
{
    sealed class UiaCache { public DateTime Time; public List<string> Names = new(); }
    static readonly Dictionary<IntPtr, UiaCache> _uia = new();

    public static List<TopMenu>? GetWin32Menus(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        IntPtr hMenu = Native.GetMenu(hwnd);
        if (hMenu == IntPtr.Zero || !Native.IsMenu(hMenu)) return null;
        int n = Native.GetMenuItemCount(hMenu);
        if (n <= 0) return null;
        var list = new List<TopMenu>();
        for (int i = 0; i < n; i++)
        {
            var info = Read(hMenu, i);
            if (info == null || info.Value.separator || string.IsNullOrWhiteSpace(info.Value.text)) continue;
            var (text, _) = SplitAccel(info.Value.text);
            IntPtr sub = info.Value.sub;
            uint id = info.Value.id;
            int pos = i;
            if (sub != IntPtr.Zero)
                list.Add(new TopMenu { Title = text, Build = () => BuildFromHMenu(hwnd, hMenu, sub, pos) });
            else
                list.Add(new TopMenu { Title = text, Invoke = () => { WindowActions.Activate(hwnd); Native.PostMessage(hwnd, Native.WM_COMMAND, new IntPtr(id), IntPtr.Zero); } });
        }
        return list.Count > 0 ? list : null;
    }

    struct ItemInfo { public string text; public uint id; public IntPtr sub; public bool separator, disabled, isChecked, ownerDraw; }

    static ItemInfo? Read(IntPtr hMenu, int pos)
    {
        var mii = new MENUITEMINFO { cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(), fMask = Native.MIIM_FTYPE | Native.MIIM_STATE | Native.MIIM_ID | Native.MIIM_SUBMENU | Native.MIIM_STRING };
        if (!Native.GetMenuItemInfo(hMenu, (uint)pos, true, ref mii)) return null;
        var info = new ItemInfo
        {
            id = mii.wID,
            sub = mii.hSubMenu,
            separator = (mii.fType & Native.MFT_SEPARATOR) != 0,
            disabled = (mii.fState & Native.MFS_DISABLED) != 0,
            isChecked = (mii.fState & Native.MFS_CHECKED) != 0,
            ownerDraw = (mii.fType & Native.MFT_OWNERDRAW) != 0,
            text = ""
        };
        if (!info.separator && mii.cch > 0)
        {
            uint cch = mii.cch + 1;
            IntPtr buf = Marshal.AllocHGlobal((int)cch * 2);
            try
            {
                var m2 = new MENUITEMINFO { cbSize = mii.cbSize, fMask = Native.MIIM_STRING, dwTypeData = buf, cch = cch };
                if (Native.GetMenuItemInfo(hMenu, (uint)pos, true, ref m2)) info.text = Marshal.PtrToStringUni(buf) ?? "";
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        return info;
    }

    static (string text, string accel) SplitAccel(string s)
    {
        string accel = "";
        int tab = s.IndexOf('\t');
        if (tab >= 0) { accel = s.Substring(tab + 1); s = s.Substring(0, tab); }
        // bỏ ký tự & (mnemonic), giữ && thành &
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '&') { if (i + 1 < s.Length && s[i + 1] == '&') { sb.Append('&'); i++; } continue; }
            sb.Append(s[i]);
        }
        return (sb.ToString().Trim(), accel);
    }

    static ContextMenu BuildFromHMenu(IntPtr hwnd, IntPtr root, IntPtr sub, int pos)
    {
        Native.SendMessageTimeout(hwnd, Native.WM_INITMENU, root, IntPtr.Zero, Native.SMTO_ABORTIFHUNG, 300, out _);
        var cm = new ContextMenu();
        Fill(cm, hwnd, sub, pos);
        return cm;
    }

    static void Fill(ItemsControl target, IntPtr hwnd, IntPtr hMenu, int pos)
    {
        target.Items.Clear();
        Native.SendMessageTimeout(hwnd, Native.WM_INITMENUPOPUP, hMenu, Native.MakeLParam(pos, 0), Native.SMTO_ABORTIFHUNG, 300, out _);
        int n = Native.GetMenuItemCount(hMenu);
        bool lastSep = true;
        for (int i = 0; i < n && i < 200; i++)
        {
            var info = Read(hMenu, i);
            if (info == null) continue;
            var it = info.Value;
            if (it.separator)
            {
                if (!lastSep) target.Items.Add(new Separator());
                lastSep = true;
                continue;
            }
            var (text, accel) = SplitAccel(it.text);
            if (string.IsNullOrWhiteSpace(text)) text = it.ownerDraw ? L.F("(mục {0})", i + 1) : "";
            if (string.IsNullOrWhiteSpace(text)) continue;
            var mi = new MenuItem { Header = text.Replace("_", "__"), InputGestureText = accel, IsEnabled = !it.disabled, IsChecked = it.isChecked };
            if (it.sub != IntPtr.Zero)
            {
                IntPtr subMenu = it.sub; int subPos = i;
                mi.Items.Add(new MenuItem { Header = "…", IsEnabled = false });
                bool loaded = false;
                mi.SubmenuOpened += (s, e) =>
                {
                    if (e.OriginalSource != mi || loaded) return;
                    loaded = true;
                    Fill(mi, hwnd, subMenu, subPos);
                };
            }
            else
            {
                uint id = it.id;
                mi.Click += (_, _) =>
                {
                    WindowActions.Activate(hwnd);
                    Native.PostMessage(hwnd, Native.WM_COMMAND, new IntPtr(id & 0xFFFF), IntPtr.Zero);
                };
            }
            target.Items.Add(mi);
            lastSep = false;
        }
        if (target.Items.Count > 0 && target.Items[^1] is Separator) target.Items.RemoveAt(target.Items.Count - 1);
        if (target.Items.Count == 0) target.Items.Add(new MenuItem { Header = L.T("(trống)"), IsEnabled = false });
    }

    // ---------------- UI Automation ----------------

    /// <summary>Tìm thanh menu qua UI Automation (chạy nền, có giới hạn thời gian).</summary>
    public static async Task<List<TopMenu>?> GetUiaMenus(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        lock (_uia)
        {
            if (_uia.TryGetValue(hwnd, out var c) && (DateTime.Now - c.Time).TotalSeconds < 30)
                return c.Names.Count > 0 ? ToMenus(hwnd, c.Names) : null;
        }
        var task = Task.Run(() => FindMenuNames(hwnd));
        var done = await Task.WhenAny(task, Task.Delay(1500));
        List<string> names = done == task && task.Result != null ? task.Result : new List<string>();
        lock (_uia)
        {
            if (_uia.Count > 50) _uia.Clear();
            _uia[hwnd] = new UiaCache { Time = DateTime.Now, Names = names };
        }
        return names.Count > 0 ? ToMenus(hwnd, names) : null;
    }

    static List<TopMenu> ToMenus(IntPtr hwnd, List<string> names) =>
        names.Select(n => new TopMenu { Title = n, Invoke = () => InvokeUia(hwnd, n) }).ToList();

    static AutomationElement? FindMenuBar(IntPtr hwnd)
    {
        var root = AutomationElement.FromHandle(hwnd);
        var walker = TreeWalker.ControlViewWalker;
        var queue = new Queue<(AutomationElement el, int depth)>();
        queue.Enqueue((root, 0));
        int visited = 0;
        while (queue.Count > 0 && visited < 400)
        {
            var (el, depth) = queue.Dequeue();
            visited++;
            AutomationElement? child = null;
            try { child = walker.GetFirstChild(el); } catch { }
            while (child != null)
            {
                try
                {
                    var ct = child.Current.ControlType;
                    if (ct == ControlType.MenuBar && child.Current.Name != "System" && !child.Current.AutomationId.Equals("SystemMenuBar", StringComparison.OrdinalIgnoreCase))
                        return child;
                    if (depth < 6 && ct != ControlType.Document && ct != ControlType.List && ct != ControlType.Tree && ct != ControlType.DataGrid && ct != ControlType.Table)
                        queue.Enqueue((child, depth + 1));
                }
                catch { }
                try { child = walker.GetNextSibling(child); } catch { child = null; }
            }
        }
        return null;
    }

    static List<string>? FindMenuNames(IntPtr hwnd)
    {
        try
        {
            var bar = FindMenuBar(hwnd);
            if (bar == null) return null;
            var names = new List<string>();
            var walker = TreeWalker.ControlViewWalker;
            var c = walker.GetFirstChild(bar);
            while (c != null && names.Count < 20)
            {
                if (c.Current.ControlType == ControlType.MenuItem && !string.IsNullOrWhiteSpace(c.Current.Name)) names.Add(c.Current.Name.Replace("&", ""));
                c = walker.GetNextSibling(c);
            }
            return names;
        }
        catch (Exception ex) { Log.Debug("UIA: " + ex.Message); return null; }
    }

    static void InvokeUia(IntPtr hwnd, string name)
    {
        WindowActions.Activate(hwnd);
        Task.Run(() =>
        {
            try
            {
                Thread.Sleep(80);
                var bar = FindMenuBar(hwnd);
                if (bar == null) return;
                var item = bar.FindFirst(TreeScope.Children, new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                    new PropertyCondition(AutomationElement.NameProperty, name)));
                if (item == null)
                {
                    var walker = TreeWalker.ControlViewWalker;
                    var c = walker.GetFirstChild(bar);
                    while (c != null) { if (c.Current.Name.Replace("&", "") == name) { item = c; break; } c = walker.GetNextSibling(c); }
                }
                if (item == null) return;
                if (item.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object p)) ((ExpandCollapsePattern)p).Expand();
                else if (item.TryGetCurrentPattern(InvokePattern.Pattern, out object ip)) ((InvokePattern)ip).Invoke();
            }
            catch (Exception ex) { Log.Debug("UIA invoke: " + ex.Message); }
        });
    }

    // ---------------- Menu chung ----------------

    const ushort VK_N = 0x4E, VK_O = 0x4F, VK_S = 0x53, VK_P = 0x50, VK_Z = 0x5A, VK_Y = 0x59, VK_X = 0x58, VK_C = 0x43, VK_V = 0x56, VK_A = 0x41, VK_F = 0x46,
        VK_OEM_PLUS = 0xBB, VK_OEM_MINUS = 0xBD, VK_0 = 0x30, VK_F11 = 0x7A, VK_F5 = 0x74;

    public static List<TopMenu> Generic(IntPtr hwnd, AppId? app)
    {
        MenuItem K(string t, string g, params ushort[] keys) { var mi = DockModel.Mi(t, () => WindowActions.SendKeysTo(hwnd, keys)); mi.InputGestureText = g; return mi; }
        var list = new List<TopMenu>
        {
            new() { Title = L.T("Tệp"), Build = () => new ContextMenu { Items = {
                K(L.T("Cửa sổ mới"), "Ctrl+N", Native.VK_CONTROL, VK_N), K(L.T("Mở…"), "Ctrl+O", Native.VK_CONTROL, VK_O), K(L.T("Lưu"), "Ctrl+S", Native.VK_CONTROL, VK_S),
                K("In…", "Ctrl+P", Native.VK_CONTROL, VK_P), new Separator(), K(L.T("Đóng cửa sổ"), "Alt+F4", Native.VK_MENU, Native.VK_F4) } } },
            new() { Title = L.T("Sửa"), Build = () => new ContextMenu { Items = {
                K(L.T("Hoàn tác"), "Ctrl+Z", Native.VK_CONTROL, VK_Z), K(L.T("Làm lại"), "Ctrl+Y", Native.VK_CONTROL, VK_Y), new Separator(),
                K(L.T("Cắt"), "Ctrl+X", Native.VK_CONTROL, VK_X), K(L.T("Sao chép"), "Ctrl+C", Native.VK_CONTROL, VK_C), K(L.T("Dán"), "Ctrl+V", Native.VK_CONTROL, VK_V),
                K(L.T("Chọn tất cả"), "Ctrl+A", Native.VK_CONTROL, VK_A), new Separator(), K(L.T("Tìm…"), "Ctrl+F", Native.VK_CONTROL, VK_F) } } },
            new() { Title = L.T("Xem"), Build = () => new ContextMenu { Items = {
                K(L.T("Phóng to"), "Ctrl++", Native.VK_CONTROL, VK_OEM_PLUS), K(L.T("Thu nhỏ"), "Ctrl+-", Native.VK_CONTROL, VK_OEM_MINUS), K(L.T("Kích thước thật"), "Ctrl+0", Native.VK_CONTROL, VK_0),
                new Separator(), K(L.T("Tải lại"), "F5", VK_F5), K(L.T("Toàn màn hình"), "F11", VK_F11) } } },
            new() { Title = L.T("Cửa sổ"), Build = () => WindowMenu(hwnd, app) },
            new() { Title = L.T("Trợ giúp"), Build = () => new ContextMenu { Items = { K(L.T("Trợ giúp của ứng dụng"), "F1", Native.VK_F1),
                DockModel.Mi(L.T("Mẹo dùng Windows"), () => Launcher.Open("ms-get-started:")) } } },
        };
        return list;
    }

    public static ContextMenu WindowMenu(IntPtr hwnd, AppId? app)
    {
        var cm = new ContextMenu();
        cm.Items.Add(DockModel.Mi(L.T("Thu nhỏ"), () => WindowActions.Minimize(hwnd), hwnd != IntPtr.Zero));
        cm.Items.Add(DockModel.Mi(L.T("Phóng to"), () => WindowActions.Maximize(hwnd), hwnd != IntPtr.Zero));
        cm.Items.Add(DockModel.Mi(L.T("Khôi phục kích thước"), () => WindowActions.RestoreWin(hwnd), hwnd != IntPtr.Zero));
        if (app != null)
        {
            var wins = WindowTracker.Instance.WindowsOf(app.Key);
            if (wins.Count > 0)
            {
                cm.Items.Add(new Separator());
                foreach (var w in wins.OrderBy(w => w.Hwnd.ToInt64()))
                {
                    var mi = DockModel.Mi(w.Title.Length > 60 ? w.Title[..57] + "…" : w.Title, () => WindowActions.Activate(w.Hwnd));
                    mi.IsChecked = w.Hwnd == hwnd;
                    cm.Items.Add(mi);
                }
            }
        }
        return cm;
    }
}
