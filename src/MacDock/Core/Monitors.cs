using System.Runtime.InteropServices;

namespace MacDock;

public sealed class MonitorInfo
{
    public IntPtr Handle { get; init; }
    public string Device { get; init; } = "";
    public RECT Bounds { get; init; }
    public RECT WorkArea { get; init; }
    public bool Primary { get; init; }
    public double Scale { get; init; } = 1.0;
    public override string ToString() => $"{Device} {Bounds} scale={Scale}{(Primary ? L.T(" (chính)") : "")}";
}

public static class Monitors
{
    public static List<MonitorInfo> GetAll()
    {
        var list = new List<MonitorInfo>();
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref RECT rc, IntPtr d) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (Native.GetMonitorInfo(h, ref mi))
            {
                double scale = 1;
                try { if (Native.GetDpiForMonitor(h, 0, out uint dx, out _) == 0) scale = dx / 96.0; } catch { }
                list.Add(new MonitorInfo
                {
                    Handle = h,
                    Device = mi.szDevice,
                    Bounds = mi.rcMonitor,
                    WorkArea = mi.rcWork,
                    Primary = (mi.dwFlags & Native.MONITORINFOF_PRIMARY) != 0,
                    Scale = scale
                });
            }
            return true;
        }, IntPtr.Zero);
        list.Sort((a, b) => a.Primary == b.Primary ? a.Bounds.Left.CompareTo(b.Bounds.Left) : (a.Primary ? -1 : 1));
        return list;
    }

    public static MonitorInfo? FromHandle(IntPtr hmon) => GetAll().FirstOrDefault(m => m.Handle == hmon);

    public static RECT WorkAreaOf(IntPtr hmon)
    {
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        return Native.GetMonitorInfo(hmon, ref mi) ? mi.rcWork : default;
    }

    public static RECT BoundsOf(IntPtr hmon)
    {
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        return Native.GetMonitorInfo(hmon, ref mi) ? mi.rcMonitor : default;
    }
}

/// <summary>Đăng ký một cửa sổ làm AppBar để chừa chỗ trên cạnh màn hình.</summary>
public sealed class AppBar : IDisposable
{
    readonly IntPtr _hwnd;
    public uint CallbackMessage { get; }
    bool _registered, _hasPos;
    public uint Edge { get; private set; }
    public RECT Reserved { get; private set; }

    public AppBar(IntPtr hwnd)
    {
        _hwnd = hwnd;
        CallbackMessage = Native.RegisterWindowMessage("MacDock.AppBarMsg");
    }

    public bool IsRegistered => _registered;

    void Register()
    {
        if (_registered) return;
        var abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = _hwnd, uCallbackMessage = CallbackMessage };
        Native.SHAppBarMessage(Native.ABM_NEW, ref abd); // thất bại nếu đã đăng ký: vẫn coi là đã đăng ký
        _registered = true;
    }

    /// <summary>Chừa một dải dày <paramref name="thickness"/> pixel trên cạnh <paramref name="edge"/> của màn hình. Trả về vùng được cấp.</summary>
    public RECT SetPosition(uint edge, RECT monitor, int thickness)
    {
        Register();
        Edge = edge;
        var abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = _hwnd, uEdge = edge, rc = monitor };
        switch (edge)
        {
            case Native.ABE_TOP: abd.rc.Bottom = abd.rc.Top + thickness; break;
            case Native.ABE_BOTTOM: abd.rc.Top = abd.rc.Bottom - thickness; break;
            case Native.ABE_LEFT: abd.rc.Right = abd.rc.Left + thickness; break;
            case Native.ABE_RIGHT: abd.rc.Left = abd.rc.Right - thickness; break;
        }
        Native.SHAppBarMessage(Native.ABM_QUERYPOS, ref abd);
        switch (edge)
        {
            case Native.ABE_TOP: abd.rc.Bottom = abd.rc.Top + thickness; break;
            case Native.ABE_BOTTOM: abd.rc.Top = abd.rc.Bottom - thickness; break;
            case Native.ABE_LEFT: abd.rc.Right = abd.rc.Left + thickness; break;
            case Native.ABE_RIGHT: abd.rc.Left = abd.rc.Right - thickness; break;
        }
        // Không đổi thì không SETPOS (tránh các AppBar báo ABN_POSCHANGED cho nhau liên tục)
        if (_hasPos && abd.rc.ToString() == Reserved.ToString()) return Reserved;
        Native.SHAppBarMessage(Native.ABM_SETPOS, ref abd);
        _hasPos = true;
        Reserved = abd.rc;
        return abd.rc;
    }

    /// <summary>Explorer khởi động lại (vd lúc đăng nhập): đăng ký AppBar lại từ đầu.</summary>
    public void Reregister() { _registered = false; _hasPos = false; }

    public void NotifyPosChanged()
    {
        if (!_registered) return;
        var abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = _hwnd };
        Native.SHAppBarMessage(Native.ABM_WINDOWPOSCHANGED, ref abd);
    }

    public void Unregister()
    {
        if (!_registered) return;
        var abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = _hwnd };
        Native.SHAppBarMessage(Native.ABM_REMOVE, ref abd);
        _registered = false;
        _hasPos = false;
    }

    public void Dispose() => Unregister();
}
