using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Threading;

namespace MacDock;

public sealed class TrayIcon
{
    public IntPtr HWnd { get; set; }
    public uint UID { get; set; }
    public Guid? Guid { get; set; }
    public uint CallbackMessage { get; set; }
    public uint Version { get; set; }
    public string Tip { get; set; } = "";
    public ImageSource? Icon { get; set; }
    public bool Hidden { get; set; }
    public uint Pid { get; set; }
    public string ExeName { get; set; } = "";
    public long Order { get; set; }

    /// <summary>Icon hệ thống của Explorer trùng với chỉ báo riêng của Thanh Menu (âm lượng, pin, mạng).</summary>
    public bool IsSystemDuplicate =>
        ExeName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) && (UID == 100 || UID == 1225 || UID == 1226 ||
        System.Text.RegularExpressions.Regex.IsMatch(Tip, "(?i)volume|speaker|headphone|battery|loa|âm lượng|tai nghe|pin |pin:|network|internet|mạng"));

    public string PersistKey => ExeName.ToLowerInvariant() + "|" + (Guid?.ToString() ?? UID.ToString());
    public override string ToString() => $"{ExeName} [{Tip}] uid={UID} v={Version}";
}

/// <summary>
/// Làm "chủ" khay hệ thống: tạo một cửa sổ lớp Shell_TrayWnd (luôn ở trên cùng thứ tự Z để
/// Shell_NotifyIcon tìm thấy trước Explorer), nhận WM_COPYDATA của các ứng dụng, đồng thời
/// chuyển tiếp nguyên vẹn cho taskbar của Explorer để khi MacDock thoát mọi thứ vẫn đúng.
/// Chạy trên luồng riêng có vòng lặp thông điệp riêng: nếu luồng giao diện bị treo, các ứng dụng
/// khác (và việc khôi phục taskbar) vẫn không bị chặn.
/// </summary>
public sealed class TrayService
{
    public static TrayService Instance { get; } = new();

    const int SIGNATURE = 0x34753423;
    const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETFOCUS = 3, NIM_SETVERSION = 4;
    const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_STATE = 8, NIF_GUID = 0x20;
    const uint NIS_HIDDEN = 1;
    const string ClassName = "Shell_TrayWnd";

    readonly object _lock = new();
    readonly List<TrayIcon> _icons = new();

    /// <summary>Bản sao danh sách icon (an toàn đa luồng).</summary>
    public List<TrayIcon> Icons { get { lock (_lock) return _icons.ToList(); } }

    /// <summary>Phát trên luồng giao diện.</summary>
    public event Action? Changed;
    public bool Running => _hwnd != IntPtr.Zero;

    /// <summary>Hàm (chạy trên luồng UI) trả về vị trí icon trên Menu Bar, để trả lời Shell_NotifyIconGetRect.</summary>
    public Func<TrayIcon, RECT?>? IconRectProvider { get; set; }

    volatile IntPtr _hwnd;
    IntPtr _notifyChild;
    WndProcDelegate? _proc;
    uint _wmTaskbarCreated;
    Thread? _thread;
    System.Threading.Timer? _timer;
    Dispatcher? _ui;
    IntPtr _explorerTray;
    long _order;
    int _changedPending;

    public void Start(Dispatcher ui)
    {
        if (_thread != null) return;
        _ui = ui;
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() => ThreadMain(ready)) { IsBackground = true, Name = "TrayHost" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait(3000);
    }

    void ThreadMain(ManualResetEventSlim ready)
    {
        try
        {
            _wmTaskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
            _proc = WndProc;
            IntPtr hInst = Native.GetModuleHandle(null);
            var wc = new WNDCLASSEX { cbSize = Marshal.SizeOf<WNDCLASSEX>(), lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc), hInstance = hInst, lpszClassName = ClassName };
            if (Native.RegisterClassEx(ref wc) == 0) { Log.Error("RegisterClassEx Shell_TrayWnd thất bại: " + Marshal.GetLastWin32Error()); ready.Set(); _thread = null; return; }
            var hwnd = Native.CreateWindowEx((uint)(Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW), ClassName, "", (uint)Native.WS_POPUP, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);
            if (hwnd == IntPtr.Zero) { Log.Error("Không tạo được cửa sổ khay hệ thống"); ready.Set(); _thread = null; return; }

            var wc2 = new WNDCLASSEX { cbSize = Marshal.SizeOf<WNDCLASSEX>(), lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc), hInstance = hInst, lpszClassName = "TrayNotifyWnd" };
            Native.RegisterClassEx(ref wc2);
            _notifyChild = Native.CreateWindowEx(0, "TrayNotifyWnd", "", (uint)Native.WS_CHILD, 0, 0, 0, 0, hwnd, IntPtr.Zero, hInst, IntPtr.Zero);

            Native.ChangeWindowMessageFilterEx(hwnd, Native.WM_COPYDATA, Native.MSGFLT_ALLOW, IntPtr.Zero);
            Native.ChangeWindowMessageFilterEx(hwnd, _wmTaskbarCreated, Native.MSGFLT_ALLOW, IntPtr.Zero);
            _hwnd = hwnd;
            MakeTopmost();
            _timer = new System.Threading.Timer(_ => Maintain(), null, 1000, 1000);
            ready.Set();

            // Yêu cầu mọi ứng dụng đăng ký lại icon (giống khi Explorer khởi động lại)
            Native.SendNotifyMessage(Native.HWND_BROADCAST, _wmTaskbarCreated, IntPtr.Zero, IntPtr.Zero);
            Log.Info("Khay hệ thống đã khởi động");

            while (Native.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                Native.TranslateMessage(ref msg);
                Native.DispatchMessage(ref msg);
            }
            Native.UnregisterClass("TrayNotifyWnd", hInst);
            Native.UnregisterClass(ClassName, hInst);
        }
        catch (Exception ex) { Log.Error("Luồng khay hệ thống lỗi", ex); ready.Set(); }
    }

    public void Stop()
    {
        var t = _thread;
        if (t == null) return;
        _timer?.Dispose(); _timer = null;
        IntPtr h = _hwnd;
        if (h != IntPtr.Zero) Native.PostMessage(h, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        t.Join(1500);
        _thread = null;
        _hwnd = IntPtr.Zero;
        lock (_lock) _icons.Clear();
        // Báo các ứng dụng đăng ký lại icon với Explorer
        Native.SendNotifyMessage(Native.HWND_BROADCAST, Native.RegisterWindowMessage("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
        Log.Info("Khay hệ thống đã dừng");
    }

    static readonly object _bypassLock = new();

    /// <summary>
    /// Gọi SHAppBarMessage của chính MacDock thẳng tới Explorer: tạm đưa cửa sổ khay của mình xuống dưới
    /// để FindWindow("Shell_TrayWnd") trả về taskbar của Explorer, gọi xong thì đưa lên lại.
    /// (Yêu cầu AppBar đi vòng qua cửa sổ khay cùng tiến trình thì Explorer nhận nhưng không cập nhật vùng làm việc.)
    /// </summary>
    public T Direct<T>(Func<T> call)
    {
        var h = _hwnd;
        if (h == IntPtr.Zero) return call();
        lock (_bypassLock)
        {
            var ex = ExplorerTray();
            Native.SetWindowPos(h, Native.HWND_BOTTOM, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            if (ex != IntPtr.Zero) Native.SetWindowPos(ex, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            try { return call(); }
            finally { MakeTopmost(); }
        }
    }

    void MakeTopmost()
    {
        var h = _hwnd;
        if (h != IntPtr.Zero) Native.SetWindowPos(h, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    void Maintain()
    {
        try
        {
            if (_hwnd == IntPtr.Zero) return;
            // Giữ cửa sổ của mình được FindWindow tìm thấy trước Explorer
            if (Native.FindWindow(ClassName, null) != _hwnd) MakeTopmost();
            bool removed;
            lock (_lock) removed = _icons.RemoveAll(i => !Native.IsWindow(i.HWnd)) > 0;
            if (removed) RaiseChanged();
        }
        catch { }
    }

    IntPtr ExplorerTray()
    {
        if (_explorerTray == IntPtr.Zero || !Native.IsWindow(_explorerTray)) _explorerTray = TaskbarController.ExplorerTray();
        return _explorerTray;
    }

    IntPtr Forward(uint msg, IntPtr wParam, IntPtr lParam)
    {
        var target = ExplorerTray();
        if (target == IntPtr.Zero || target == _hwnd) return IntPtr.Zero;
        Native.SendMessageTimeout(target, msg, wParam, lParam, Native.SMTO_ABORTIFHUNG, 1000, out IntPtr result);
        return result;
    }

    IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (hWnd == _hwnd || (_hwnd == IntPtr.Zero && msg == Native.WM_COPYDATA))
            {
                if (msg == Native.WM_COPYDATA && lParam != IntPtr.Zero)
                {
                    var cds = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);
                    long kind = cds.dwData.ToInt64();
                    if (kind == 1 && cds.cbData >= 8 && cds.lpData != IntPtr.Zero && Marshal.ReadInt32(cds.lpData) == SIGNATURE)
                    {
                        bool ok = HandleNotify(cds);
                        Forward(msg, wParam, lParam);
                        return ok ? new IntPtr(1) : IntPtr.Zero;
                    }
                    if (kind == 3 && cds.cbData >= 40 && cds.lpData != IntPtr.Zero && Marshal.ReadInt32(cds.lpData) == SIGNATURE)
                    {
                        var r = HandleGetRect(cds);
                        if (r != IntPtr.Zero) return r;
                    }
                    return Forward(msg, wParam, lParam);
                }
                if (msg == Native.WM_CLOSE) { Native.DestroyWindow(hWnd); return IntPtr.Zero; }
                if (msg == Native.WM_DESTROY) { Native.PostQuitMessage(0); return IntPtr.Zero; }
                if (msg == _wmTaskbarCreated) return IntPtr.Zero;
            }
        }
        catch (Exception ex) { Log.Error("Tray WndProc", ex); }
        return Native.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    TrayIcon? Find(IntPtr hwnd, uint uid, Guid? guid)
    {
        if (guid is { } g && g != System.Guid.Empty)
        {
            var byGuid = _icons.FirstOrDefault(i => i.Guid == g);
            if (byGuid != null) return byGuid;
        }
        return _icons.FirstOrDefault(i => i.HWnd == hwnd && i.UID == uid);
    }

    bool HandleNotify(COPYDATASTRUCT cds)
    {
        uint message = (uint)Marshal.ReadInt32(cds.lpData, 4);
        int len = Math.Min(cds.cbData - 8, 956);
        var buf = new byte[956];
        Marshal.Copy(cds.lpData + 8, buf, 0, len);

        uint U(int off) => off + 4 <= len ? BitConverter.ToUInt32(buf, off) : 0;
        string S(int off, int chars)
        {
            int max = Math.Min(chars * 2, Math.Max(0, len - off));
            if (max <= 0) return "";
            var s = System.Text.Encoding.Unicode.GetString(buf, off, max);
            int z = s.IndexOf('\0');
            return z >= 0 ? s.Substring(0, z) : s;
        }

        IntPtr hwnd = new((int)U(4));
        uint uid = U(8), flags = U(12), cbMsg = U(16);
        IntPtr hIcon = new((int)U(20));
        Guid? guid = null;
        if ((flags & NIF_GUID) != 0 && len >= 952)
        {
            var gb = new byte[16]; Array.Copy(buf, 936, gb, 0, 16);
            guid = new Guid(gb);
        }

        // chuyển icon (nặng nhất) ra ngoài khoá
        ImageSource? newIcon = null;
        bool iconGiven = (flags & NIF_ICON) != 0 && (message == NIM_ADD || message == NIM_MODIFY);
        if (iconGiven && hIcon != IntPtr.Zero)
        {
            IntPtr copy = Native.CopyIcon(hIcon);
            if (copy != IntPtr.Zero) { newIcon = ShellIcons.FromHIcon(copy); Native.DestroyIcon(copy); }
        }

        lock (_lock)
        {
            var icon = Find(hwnd, uid, guid);
            switch (message)
            {
                case NIM_ADD:
                case NIM_MODIFY:
                    bool isNew = false;
                    if (icon == null)
                    {
                        if (message == NIM_MODIFY && newIcon == null) return false;
                        Native.GetWindowThreadProcessId(hwnd, out uint pid);
                        icon = new TrayIcon { HWnd = hwnd, UID = uid, Guid = guid, Pid = pid, Order = ++_order };
                        var path = AppIdentity.ProcessPath(pid);
                        icon.ExeName = path != null ? Path.GetFileName(path) : "pid" + pid;
                        isNew = true;
                    }
                    if (guid != null) icon.Guid = guid;
                    if ((flags & NIF_MESSAGE) != 0) icon.CallbackMessage = cbMsg;
                    if ((flags & NIF_TIP) != 0) icon.Tip = S(24, 128);
                    if ((flags & NIF_STATE) != 0)
                    {
                        uint state = U(280), mask = U(284);
                        if ((mask & NIS_HIDDEN) != 0) icon.Hidden = (state & NIS_HIDDEN) != 0;
                    }
                    if (iconGiven) icon.Icon = newIcon;
                    if (isNew) { _icons.Add(icon); Log.Debug("Tray + " + icon); }
                    break;

                case NIM_DELETE:
                    if (icon == null) return false;
                    _icons.Remove(icon);
                    break;

                case NIM_SETVERSION:
                    if (icon == null) return false;
                    icon.Version = U(800);
                    return true;

                case NIM_SETFOCUS:
                    return true;

                default:
                    return false;
            }
        }
        RaiseChanged();
        return true;
    }

    IntPtr HandleGetRect(COPYDATASTRUCT cds)
    {
        // WINNOTIFYICONIDENTIFIER: magic, message(1=trái-trên, 2=phải-dưới), cbSize, padding, hWnd(32), uID, guid
        int message = Marshal.ReadInt32(cds.lpData, 4);
        IntPtr hwnd = new(Marshal.ReadInt32(cds.lpData, 16));
        uint uid = (uint)Marshal.ReadInt32(cds.lpData, 20);
        var gb = new byte[16]; Marshal.Copy(cds.lpData + 24, gb, 0, 16);
        var g = new Guid(gb);
        TrayIcon? icon;
        lock (_lock) icon = Find(hwnd, uid, g == System.Guid.Empty ? null : g);
        if (icon == null || IconRectProvider == null || _ui == null) return IntPtr.Zero;
        RECT? r = null;
        try
        {
            var op = _ui.BeginInvoke(() => IconRectProvider(icon));
            if (op.Wait(TimeSpan.FromMilliseconds(200)) == DispatcherOperationStatus.Completed) r = op.Result as RECT?;
        }
        catch { }
        if (r == null) return IntPtr.Zero;
        int x = message == 1 ? r.Value.Left : r.Value.Right;
        int y = message == 1 ? r.Value.Top : r.Value.Bottom;
        return new IntPtr((y << 16) | (x & 0xFFFF));
    }

    void RaiseChanged()
    {
        // gộp các thay đổi liên tục (icon động) để không vẽ lại quá nhiều
        if (Interlocked.Exchange(ref _changedPending, 1) == 1) return;
        _ui?.BeginInvoke(async () =>
        {
            await Task.Delay(60);
            Interlocked.Exchange(ref _changedPending, 0);
            Changed?.Invoke();
        });
    }

    // ---------------- Gửi sự kiện chuột cho ứng dụng ----------------

    public enum Click { Left, Right, Double, Middle, Hover }

    public void SendMouse(TrayIcon icon, Click click, int x, int y)
    {
        if (!Native.IsWindow(icon.HWnd)) { lock (_lock) _icons.Remove(icon); RaiseChanged(); return; }
        if (icon.CallbackMessage == 0) return;
        if (icon.Pid != 0) Native.AllowSetForegroundWindow(icon.Pid);

        void Post(int mouseMsg)
        {
            IntPtr wParam, lParam;
            if (icon.Version >= 4)
            {
                wParam = Native.MakeLParam(x, y);
                lParam = Native.MakeLParam(mouseMsg, (int)icon.UID);
            }
            else
            {
                wParam = new IntPtr(icon.UID);
                lParam = new IntPtr(mouseMsg);
            }
            Native.PostMessage(icon.HWnd, icon.CallbackMessage, wParam, lParam);
        }

        switch (click)
        {
            case Click.Left:
                Post(Native.WM_LBUTTONDOWN); Post(Native.WM_LBUTTONUP);
                if (icon.Version >= 4) Post(Native.NIN_SELECT);
                break;
            case Click.Double:
                Post(Native.WM_LBUTTONDBLCLK); Post(Native.WM_LBUTTONUP);
                break;
            case Click.Right:
                Post(Native.WM_RBUTTONDOWN); Post(Native.WM_RBUTTONUP);
                if (icon.Version >= 4) Post(Native.WM_CONTEXTMENU);
                break;
            case Click.Middle:
                Post(Native.WM_MBUTTONDOWN); Post(Native.WM_MBUTTONUP);
                break;
            case Click.Hover:
                Post(Native.WM_MOUSEMOVE);
                break;
        }
    }
}
