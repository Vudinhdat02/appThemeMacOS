using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace MacDock;

/// <summary>
/// Đóng popup/menu khi bấm chuột ra ngoài ở bất kỳ đâu trên màn hình (kể cả trong app khác, desktop).
/// Dùng hook chuột cấp thấp trên một luồng riêng, chỉ bật khi đang có popup/menu mở.
/// </summary>
public static class OutsideClick
{
    delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, LowLevelProc proc, IntPtr hMod, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint tid, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT pt);

    sealed class Entry { public Action Close = null!; public Func<IntPtr, bool>? Inside; }

    static readonly List<Entry> _entries = new();
    static Thread? _thread;
    static uint _tid;
    static LowLevelProc? _proc;
    static Dispatcher? _ui;

    /// <summary>
    /// Theo dõi một popup. <paramref name="inside"/> nhận HWND gốc dưới con trỏ, trả về true nếu cú bấm được coi là ở trong
    /// (mặc định: mọi cửa sổ của MacDock đều tính là "trong"). Trả về hàm huỷ theo dõi.
    /// </summary>
    public static Action Watch(Action close, Func<IntPtr, bool>? inside = null)
    {
        _ui ??= System.Windows.Application.Current.Dispatcher;
        var e = new Entry { Close = close, Inside = inside };
        lock (_entries) { _entries.Add(e); if (_entries.Count == 1) Start(); }
        return () => { lock (_entries) { if (_entries.Remove(e) && _entries.Count == 0) Stop(); } };
    }

    static void Start()
    {
        _thread = new Thread(() =>
        {
            _tid = Native.GetCurrentThreadId();
            _proc = Hook;
            IntPtr h = SetWindowsHookEx(14 /*WH_MOUSE_LL*/, _proc, Native.GetModuleHandle(null), 0);
            while (Native.GetMessage(out var m, IntPtr.Zero, 0, 0) > 0) { Native.TranslateMessage(ref m); Native.DispatchMessage(ref m); }
            if (h != IntPtr.Zero) UnhookWindowsHookEx(h);
        }) { IsBackground = true, Name = "OutsideClick" };
        _thread.Start();
    }

    static void Stop()
    {
        if (_tid != 0) PostThreadMessage(_tid, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread = null; _tid = 0;
    }

    static IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg is 0x201 or 0x204 or 0x207) // WM_LBUTTONDOWN / RBUTTONDOWN / MBUTTONDOWN
            {
                var pt = Marshal.PtrToStructure<POINT>(lParam);
                IntPtr root = Native.GetAncestor(WindowFromPoint(pt), Native.GA_ROOT);
                _ui?.BeginInvoke(() => Handle(root));
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    static void Handle(IntPtr root)
    {
        Native.GetWindowThreadProcessId(root, out uint pid);
        List<Entry> list;
        lock (_entries) list = _entries.ToList();
        foreach (var e in list)
        {
            bool inside = e.Inside != null ? e.Inside(root) : pid == Environment.ProcessId;
            if (!inside) e.Close();
        }
    }

    /// <summary>Mọi ContextMenu của MacDock (menu Thanh Menu, menu Dock…) tự đóng khi bấm ra ngoài app.</summary>
    public static void HookContextMenus()
    {
        var map = new System.Runtime.CompilerServices.ConditionalWeakTable<System.Windows.Controls.ContextMenu, Action>();
        System.Windows.EventManager.RegisterClassHandler(typeof(System.Windows.Controls.ContextMenu), System.Windows.Controls.ContextMenu.OpenedEvent,
            new System.Windows.RoutedEventHandler((s, _) =>
            {
                if (s is not System.Windows.Controls.ContextMenu cm) return;
                if (map.TryGetValue(cm, out var old)) { old(); map.Remove(cm); }
                map.Add(cm, Watch(() => cm.IsOpen = false));
            }));
        System.Windows.EventManager.RegisterClassHandler(typeof(System.Windows.Controls.ContextMenu), System.Windows.Controls.ContextMenu.ClosedEvent,
            new System.Windows.RoutedEventHandler((s, _) =>
            {
                if (s is System.Windows.Controls.ContextMenu cm && map.TryGetValue(cm, out var un)) { un(); map.Remove(cm); }
            }));
    }

    /// <summary>Đóng tất cả popup đang theo dõi (vd khi bấm Esc).</summary>
    public static void CloseAll()
    {
        List<Entry> list;
        lock (_entries) list = _entries.ToList();
        foreach (var e in list) e.Close();
    }
}
