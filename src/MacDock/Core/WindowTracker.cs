using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace MacDock;

public sealed class AppWindow
{
    public IntPtr Hwnd { get; init; }
    public AppId Id { get; set; } = null!;
    public string Title { get; set; } = "";
    public bool Minimized { get; set; }
    public bool Flashing { get; set; }
    public int TitleBadge { get; set; }
    public DateTime BadgeSeen { get; set; }

    /// <summary>Cập nhật huy hiệu từ tiêu đề; giữ số cũ 10 giây khi tiêu đề nhấp nháy (vd Messenger). Trả về true nếu đổi.</summary>
    public bool UpdateBadge(int parsed)
    {
        if (parsed > 0) BadgeSeen = DateTime.Now;
        else if (TitleBadge > 0 && (DateTime.Now - BadgeSeen).TotalSeconds < 10) return false;
        if (parsed == TitleBadge) return false;
        TitleBadge = parsed;
        return true;
    }
    public long LastActive { get; set; }
}

/// <summary>
/// Theo dõi các cửa sổ ứng dụng (kiểu Alt-Tab). Dựa vào shell hook + SetWinEventHook, chỉ quét lại
/// toàn bộ (EnumWindows, ~1ms) khi có sự kiện, kèm quét dự phòng mỗi 10 giây.
/// </summary>
public sealed class WindowTracker
{
    public static WindowTracker Instance { get; } = new();

    readonly Dictionary<IntPtr, AppWindow> _windows = new();
    public IReadOnlyCollection<AppWindow> Windows => _windows.Values;

    public event Action? Changed;
    public event Action? ForegroundChanged;
    public event Action<AppWindow>? Attention;

    public IntPtr Foreground { get; private set; }
    public AppId? ForegroundApp { get; private set; }
    public IntPtr ForegroundAppHwnd { get; private set; }

    readonly DispatcherTimer _debounce;
    readonly DispatcherTimer _fallback;
    readonly List<IntPtr> _hooks = new();
    readonly WinEventDelegate _hookProc;
    long _activeCounter;
    static readonly Regex _badgeRx = new(@"^\s*[\(\[](\d{1,4})\+?[\)\]]", RegexOptions.Compiled);

    static readonly HashSet<string> _ignoredClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "XamlExplorerHostIslandWindow", "ForegroundStaging",
        "MultitaskingViewFrame", "TaskListThumbnailWnd", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland",
        "#32768", "Windows.UI.Input.InputSite.WindowClass", "SysShadow", "tooltips_class32", "Shell_InputSwitchTopLevelWindow",
        "MacDock.TrayHost", "ThumbnailDeviceHelperWnd", "EdgeUiInputTopWndClass", "NativeHWNDHost", "ApplicationManager_DesktopShellWindow"
    };

    static readonly HashSet<string> _shellProcs = new(StringComparer.OrdinalIgnoreCase)
    {
        "StartMenuExperienceHost.exe", "SearchHost.exe", "SearchApp.exe", "ShellExperienceHost.exe", "ShellHost.exe", "LockApp.exe",
        "TextInputHost.exe", "Widgets.exe", "WidgetBoard.exe"
    };

    WindowTracker()
    {
        _hookProc = OnWinEvent;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Rescan(); };
        _fallback = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _fallback.Tick += (_, _) => Rescan();
    }

    public void Start()
    {
        uint flags = Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS;
        void Hook(uint a, uint b) { var h = Native.SetWinEventHook(a, b, IntPtr.Zero, _hookProc, 0, 0, flags); if (h != IntPtr.Zero) _hooks.Add(h); }
        // Không bỏ qua tiến trình của mình với foreground để menu bar biết khi cửa sổ Cài đặt được kích hoạt
        var fh = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _hookProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
        if (fh != IntPtr.Zero) _hooks.Add(fh);
        Hook(Native.EVENT_SYSTEM_MINIMIZESTART, Native.EVENT_SYSTEM_MINIMIZEEND);
        Hook(Native.EVENT_OBJECT_CLOAKED, Native.EVENT_OBJECT_UNCLOAKED);
        Rescan();
        UpdateForeground(Native.GetForegroundWindow());
        _fallback.Start();
    }

    public void Stop()
    {
        foreach (var h in _hooks) Native.UnhookWinEvent(h);
        _hooks.Clear();
        _fallback.Stop();
    }

    void OnWinEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        Diag.WinEvents++;
        if (idObject != Native.OBJID_WINDOW || idChild != 0 || hwnd == IntPtr.Zero) return;
        if (ev == Native.EVENT_SYSTEM_FOREGROUND)
        {
            UpdateForeground(hwnd);
            if (_windows.TryGetValue(hwnd, out var w)) { w.Flashing = false; w.LastActive = ++_activeCounter; }
            ScheduleRescan();
            return;
        }
        if (ev == Native.EVENT_SYSTEM_MINIMIZESTART || ev == Native.EVENT_SYSTEM_MINIMIZEEND)
        {
            if (_windows.TryGetValue(hwnd, out var w)) w.Minimized = ev == Native.EVENT_SYSTEM_MINIMIZESTART;
            ScheduleRescan();
            return;
        }
        // cloak/uncloak: chỉ quan tâm cửa sổ cấp cao nhất
        if (Native.GetAncestor(hwnd, Native.GA_PARENT) == Native.GetDesktopWindow()) ScheduleRescan();
    }

    /// <summary>Nhận thông điệp shell hook từ cửa sổ ẩn của App.</summary>
    public void OnShellHook(int code, IntPtr hwnd)
    {
        Diag.ShellHooks++;
        if (Log.Verbose) Diag.Hit("hook" + (code & 0x7FFF) + ":" + Native.GetClass(hwnd));
        switch (code & 0x7FFF)
        {
            case Native.HSHELL_WINDOWCREATED:
            case Native.HSHELL_WINDOWDESTROYED:
            case Native.HSHELL_WINDOWREPLACED:
            case Native.HSHELL_MONITORCHANGED:
                if ((code & 0x7FFF) == Native.HSHELL_WINDOWDESTROYED) AppIdentity.Forget(hwnd);
                ScheduleRescan();
                break;
            case Native.HSHELL_REDRAW:
                if ((code & 0x8000) != 0) goto case 0x7FFF; // HSHELL_FLASH = REDRAW | HIGHBIT
                if (_windows.TryGetValue(hwnd, out var w))
                {
                    var t = Native.GetWindowTitle(hwnd);
                    if (t != w.Title)
                    {
                        w.Title = t;
                        if (w.UpdateBadge(ParseBadge(t))) Changed?.Invoke();
                    }
                }
                break;
            case 0x7FFF:
                if (_windows.TryGetValue(hwnd, out var fw) && Native.GetForegroundWindow() != hwnd)
                {
                    bool was = fw.Flashing;
                    fw.Flashing = true;
                    if (!was) { Attention?.Invoke(fw); Changed?.Invoke(); }
                }
                break;
            case Native.HSHELL_WINDOWACTIVATED & 0x7FFF:
                ScheduleRescan();
                break;
        }
    }

    public void ScheduleRescan()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    static int ParseBadge(string title)
    {
        var m = _badgeRx.Match(title);
        return m.Success && int.TryParse(m.Groups[1].Value, out int n) ? n : 0;
    }

    public static bool IsAppWindow(IntPtr h)
    {
        if (!Native.IsWindowVisible(h)) return false;
        long ex = Native.GetExStyle(h);
        bool appWin = (ex & Native.WS_EX_APPWINDOW) != 0;
        if ((ex & Native.WS_EX_TOOLWINDOW) != 0 && !appWin) return false;
        if ((ex & Native.WS_EX_NOACTIVATE) != 0 && !appWin) return false;
        IntPtr owner = Native.GetWindow(h, Native.GW_OWNER);
        if (owner != IntPtr.Zero && !appWin) return false;
        if (Native.IsCloaked(h)) return false;
        if (Native.GetWindowTextLength(h) == 0) return false;
        string cls = Native.GetClass(h);
        if (_ignoredClasses.Contains(cls)) return false;
        Native.GetWindowThreadProcessId(h, out uint pid);
        if (pid == Environment.ProcessId && !appWin) return false;
        return true;
    }

    public void Rescan()
    {
        Diag.Rescans++;
        var seen = new HashSet<IntPtr>();
        bool changed = false;
        Native.EnumWindows((h, _) =>
        {
            if (!IsAppWindow(h)) return true;
            var id = AppIdentity.Resolve(h);
            if (id == null) return true;
            if (id.ExePath != null && _shellProcs.Contains(Path.GetFileName(id.ExePath))) return true;
            seen.Add(h);
            string title = Native.GetWindowTitle(h);
            bool min = Native.IsIconic(h);
            if (!_windows.TryGetValue(h, out var w))
            {
                _windows[h] = new AppWindow { Hwnd = h, Id = id, Title = title, Minimized = min, TitleBadge = ParseBadge(title), LastActive = 0 };
                changed = true;
            }
            else
            {
                if (w.Id.Key != id.Key) { w.Id = id; changed = true; }
                if (w.Title != title)
                {
                    w.Title = title;
                    if (w.UpdateBadge(ParseBadge(title))) changed = true;
                }
                if (w.Minimized != min) { w.Minimized = min; changed = true; }
            }
            return true;
        }, IntPtr.Zero);

        foreach (var h in _windows.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            _windows.Remove(h);
            AppIdentity.Forget(h);
            changed = true;
        }
        if (changed) Changed?.Invoke();
    }

    void UpdateForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        Foreground = hwnd;
        string cls = Native.GetClass(hwnd);
        if (cls is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "XamlExplorerHostIslandWindow" or "ForegroundStaging" or "MultitaskingViewFrame"
            or "#32768" or "TaskListThumbnailWnd" or "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland" or "MacDock.TrayHost")
            return;
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == Environment.ProcessId)
        {
            // Chỉ cửa sổ ứng dụng thật của MacDock (Cài đặt...) mới tính
            if ((Native.GetExStyle(hwnd) & Native.WS_EX_APPWINDOW) == 0) return;
        }
        IntPtr root = Native.GetAncestor(hwnd, Native.GA_ROOTOWNER);
        if (root == IntPtr.Zero) root = hwnd;
        var id = AppIdentity.Resolve(root) ?? AppIdentity.Resolve(hwnd);
        if (id == null) return;
        if (id.ExePath != null && _shellProcs.Contains(Path.GetFileName(id.ExePath))) return;
        if (cls is "Progman" or "WorkerW")
            id = new AppId(AppIdentity.ExplorerPath.ToLowerInvariant(), AppIdentity.ExplorerPath, null, pid);
        ForegroundApp = id;
        ForegroundAppHwnd = cls is "Progman" or "WorkerW" ? IntPtr.Zero : root;
        if (_windows.TryGetValue(root, out var w)) { w.LastActive = ++_activeCounter; w.Flashing = false; }
        ForegroundChanged?.Invoke();
    }

    public List<AppWindow> WindowsOf(string key) =>
        _windows.Values.Where(w => w.Id.Key == key).OrderByDescending(w => w.LastActive).ThenBy(w => w.Hwnd.ToInt64()).ToList();
}
