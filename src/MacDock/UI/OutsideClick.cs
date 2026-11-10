using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace MacDock;

/// <summary>
/// Đóng popup/menu khi bấm chuột ra ngoài ở bất kỳ đâu trên màn hình (kể cả trong app khác, desktop).
/// Dùng hook chuột cấp thấp trên một luồng riêng; chỉ xử lý khi đang có popup/menu mở.
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
    // Một delegate duy nhất, sống suốt đời app: Windows giữ con trỏ tới nó trong lúc hook còn cài. Nếu tạo mới mỗi lần
    // bật hook, delegate cũ có thể bị GC thu hồi khi luồng hook cũ chưa kịp gỡ → app bị Windows tắt ngay, không kịp ghi log.
    static readonly LowLevelProc _proc = Hook;
    static Dispatcher? _ui;

    /// <summary>
    /// Theo dõi một popup. <paramref name="inside"/> nhận HWND gốc dưới con trỏ, trả về true nếu cú bấm được coi là ở trong
    /// (mặc định: mọi cửa sổ của MacDock đều tính là "trong"). Trả về hàm huỷ theo dõi.
    /// </summary>
    public static Action Watch(Action close, Func<IntPtr, bool>? inside = null)
    {
        _ui ??= System.Windows.Application.Current.Dispatcher;
        var e = new Entry { Close = close, Inside = inside };
        lock (_entries) { _entries.Add(e); _active = _entries.Count; }
        EnsureThread();
        return () => { lock (_entries) { _entries.Remove(e); _active = _entries.Count; } };
    }

    static volatile int _active;

    /// <summary>
    /// Một luồng hook duy nhất chạy suốt đời app (khi không có popup nào mở, hook chỉ chuyển tiếp ngay — gần như không tốn gì).
    /// Trước đây hook được bật/tắt theo từng popup: mở rồi đóng thật nhanh có thể để lại luồng hook cũ không được gỡ,
    /// delegate của nó bị GC thu hồi và Windows tắt MacDock ở cú bấm chuột kế tiếp mà không kịp ghi log.
    /// </summary>
    static void EnsureThread()
    {
        lock (_entries)
        {
            if (_thread != null) return;
            _thread = new Thread(() =>
            {
                _tid = Native.GetCurrentThreadId();
                IntPtr h = SetWindowsHookEx(14 /*WH_MOUSE_LL*/, _proc, Native.GetModuleHandle(null), 0);
                if (h == IntPtr.Zero) { Log.Warn("Không cài được hook chuột (đóng popup khi bấm ra ngoài)"); return; }
                while (Native.GetMessage(out var m, IntPtr.Zero, 0, 0) > 0) { Native.TranslateMessage(ref m); Native.DispatchMessage(ref m); }
                UnhookWindowsHookEx(h);
            }) { IsBackground = true, Name = "OutsideClick" };
            _thread.Start();
        }
    }

    static IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && _active > 0)
            {
                int msg = wParam.ToInt32();
                if (msg is 0x201 or 0x204 or 0x207) // WM_LBUTTONDOWN / RBUTTONDOWN / MBUTTONDOWN
                {
                    var pt = Marshal.PtrToStructure<POINT>(lParam);
                    IntPtr root = Native.GetAncestor(WindowFromPoint(pt), Native.GA_ROOT);
                    _ui?.BeginInvoke(() => { try { Handle(root); } catch (Exception ex) { Log.Error("OutsideClick", ex); } });
                }
            }
        }
        catch { /* không bao giờ để ngoại lệ thoát ra khỏi callback của Windows */ }
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
