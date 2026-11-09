using System.Runtime.InteropServices;
using System.Text;

namespace MacDock;

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left, Top, Right, Bottom;
    public RECT(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; }
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
    public override string ToString() => $"[{Left},{Top},{Right},{Bottom}]";
}

[StructLayout(LayoutKind.Sequential)]
public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }

[StructLayout(LayoutKind.Sequential)]
public struct APPBARDATA
{
    public int cbSize;
    public IntPtr hWnd;
    public uint uCallbackMessage;
    public uint uEdge;
    public RECT rc;
    public IntPtr lParam;
}

[StructLayout(LayoutKind.Sequential)]
public struct COPYDATASTRUCT
{
    public IntPtr dwData;
    public int cbData;
    public IntPtr lpData;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MONITORINFOEX
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
}

[StructLayout(LayoutKind.Sequential)]
public struct SYSTEM_POWER_STATUS
{
    public byte ACLineStatus;
    public byte BatteryFlag;
    public byte BatteryLifePercent;
    public byte SystemStatusFlag;
    public int BatteryLifeTime;
    public int BatteryFullLifeTime;
}

[StructLayout(LayoutKind.Sequential)]
public struct MEMORYSTATUSEX
{
    public uint dwLength;
    public uint dwMemoryLoad;
    public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MENUITEMINFO
{
    public uint cbSize;
    public uint fMask;
    public uint fType;
    public uint fState;
    public uint wID;
    public IntPtr hSubMenu;
    public IntPtr hbmpChecked;
    public IntPtr hbmpUnchecked;
    public IntPtr dwItemData;
    public IntPtr dwTypeData;
    public uint cch;
    public IntPtr hbmpItem;
}

[StructLayout(LayoutKind.Sequential)]
public struct BITMAP
{
    public int bmType, bmWidth, bmHeight, bmWidthBytes;
    public ushort bmPlanes, bmBitsPixel;
    public IntPtr bmBits;
}

[StructLayout(LayoutKind.Sequential)]
public struct BITMAPINFOHEADER
{
    public uint biSize;
    public int biWidth, biHeight;
    public ushort biPlanes, biBitCount;
    public uint biCompression, biSizeImage;
    public int biXPelsPerMeter, biYPelsPerMeter;
    public uint biClrUsed, biClrImportant;
}

[StructLayout(LayoutKind.Sequential)]
public struct INPUT
{
    public uint type;
    public InputUnion U;
}

[StructLayout(LayoutKind.Explicit)]
public struct InputUnion
{
    [FieldOffset(0)] public KEYBDINPUT ki;
    [FieldOffset(0)] public MOUSEINPUT mi;
}

[StructLayout(LayoutKind.Sequential)]
public struct KEYBDINPUT
{
    public ushort wVk, wScan;
    public uint dwFlags, time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct MOUSEINPUT
{
    public int dx, dy;
    public uint mouseData, dwFlags, time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct SHFILEOPSTRUCT
{
    public IntPtr hwnd;
    public uint wFunc;
    [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
    [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
    public ushort fFlags;
    public int fAnyOperationsAborted;
    public IntPtr hNameMappings;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
}

[StructLayout(LayoutKind.Sequential)]
public struct SHQUERYRBINFO
{
    public int cbSize;
    public long i64Size;
    public long i64NumItems;
}

[StructLayout(LayoutKind.Sequential)]
public struct WNDCLASSEX
{
    public int cbSize;
    public uint style;
    public IntPtr lpfnWndProc;
    public int cbClsExtra, cbWndExtra;
    public IntPtr hInstance, hIcon, hCursor, hbrBackground;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
    public IntPtr hIconSm;
}

[StructLayout(LayoutKind.Sequential)]
public struct PROPERTYKEY
{
    public Guid fmtid;
    public uint pid;
    public PROPERTYKEY(Guid g, uint p) { fmtid = g; pid = p; }
}

[StructLayout(LayoutKind.Explicit)]
public struct PROPVARIANT
{
    [FieldOffset(0)] public ushort vt;
    [FieldOffset(8)] public IntPtr pointerValue;
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint cProps);
    [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
    [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
    [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
    [PreserveSig] int Commit();
}

public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rc, IntPtr data);
public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

public static class Native
{
    // ---- Messages / constants ----
    public const int WM_NULL = 0x0000, WM_DESTROY = 0x0002, WM_CLOSE = 0x0010, WM_QUIT = 0x0012, WM_ACTIVATEAPP = 0x001C,
        WM_SETTINGCHANGE = 0x001A, WM_COPYDATA = 0x004A, WM_DISPLAYCHANGE = 0x007E, WM_CONTEXTMENU = 0x007B,
        WM_COMMAND = 0x0111, WM_SYSCOMMAND = 0x0112, WM_TIMER = 0x0113, WM_INITMENU = 0x0116, WM_INITMENUPOPUP = 0x0117,
        WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203,
        WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208,
        WM_MOUSEACTIVATE = 0x0021, WM_NCHITTEST = 0x0084, WM_DPICHANGED = 0x02E0, WM_POWERBROADCAST = 0x0218,
        WM_INPUTLANGCHANGEREQUEST = 0x0050, WM_WINDOWPOSCHANGING = 0x0046, WM_USER = 0x0400;
    public const int MA_NOACTIVATE = 3;
    public const int NIN_SELECT = WM_USER, NIN_KEYSELECT = WM_USER + 1, NIN_POPUPOPEN = WM_USER + 6, NIN_POPUPCLOSE = WM_USER + 7;

    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const long WS_VISIBLE = 0x10000000L, WS_CHILD = 0x40000000L, WS_POPUP = 0x80000000L, WS_CAPTION = 0x00C00000L, WS_MINIMIZEBOX = 0x00020000L;
    public const long WS_EX_TOOLWINDOW = 0x00000080L, WS_EX_APPWINDOW = 0x00040000L, WS_EX_NOACTIVATE = 0x08000000L,
        WS_EX_TOPMOST = 0x00000008L, WS_EX_TRANSPARENT = 0x00000020L, WS_EX_LAYERED = 0x00080000L, WS_EX_NOREDIRECTIONBITMAP = 0x00200000L;

    public const int SW_HIDE = 0, SW_SHOWNORMAL = 1, SW_SHOWMINIMIZED = 2, SW_MAXIMIZE = 3, SW_SHOWNOACTIVATE = 4, SW_SHOW = 5, SW_MINIMIZE = 6,
        SW_SHOWMINNOACTIVE = 7, SW_SHOWNA = 8, SW_RESTORE = 9;

    public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040,
        SWP_HIDEWINDOW = 0x0080, SWP_NOOWNERZORDER = 0x0200, SWP_ASYNCWINDOWPOS = 0x4000;
    public static readonly IntPtr HWND_TOPMOST = new(-1), HWND_NOTOPMOST = new(-2), HWND_TOP = IntPtr.Zero, HWND_BOTTOM = new(1),
        HWND_BROADCAST = new(0xffff), HWND_MESSAGE = new(-3);

    public const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3, ABM_GETSTATE = 4, ABM_GETTASKBARPOS = 5,
        ABM_ACTIVATE = 6, ABM_WINDOWPOSCHANGED = 9, ABM_SETSTATE = 10;
    public const uint ABN_STATECHANGE = 0, ABN_POSCHANGED = 1, ABN_FULLSCREENAPP = 2, ABN_WINDOWARRANGE = 3;
    public const int ABS_AUTOHIDE = 1, ABS_ALWAYSONTOP = 2;
    public const uint ABE_LEFT = 0, ABE_TOP = 1, ABE_RIGHT = 2, ABE_BOTTOM = 3;

    public const uint GA_PARENT = 1, GA_ROOT = 2, GA_ROOTOWNER = 3;
    public const uint GW_OWNER = 4;

    public const int DWMWA_CLOAKED = 14, DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_SYSTEMBACKDROP_TYPE = 38,
        DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_EXCLUDED_FROM_PEEK = 12, DWMWA_DISALLOW_PEEK = 11, DWMWA_NCRENDERING_POLICY = 2,
        DWMWA_BORDER_COLOR = 34;

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003, EVENT_SYSTEM_MINIMIZESTART = 0x0016, EVENT_SYSTEM_MINIMIZEEND = 0x0017,
        EVENT_OBJECT_DESTROY = 0x8001, EVENT_OBJECT_SHOW = 0x8002, EVENT_OBJECT_HIDE = 0x8003, EVENT_OBJECT_NAMECHANGE = 0x800C,
        EVENT_OBJECT_CLOAKED = 0x8017, EVENT_OBJECT_UNCLOAKED = 0x8018, EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public const uint WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 2;
    public const int OBJID_WINDOW = 0;

    public const int HSHELL_WINDOWCREATED = 1, HSHELL_WINDOWDESTROYED = 2, HSHELL_ACTIVATESHELLWINDOW = 3, HSHELL_WINDOWACTIVATED = 4,
        HSHELL_GETMINRECT = 5, HSHELL_REDRAW = 6, HSHELL_TASKMAN = 7, HSHELL_LANGUAGE = 8, HSHELL_FLASH = 0x8006, HSHELL_RUDEAPPACTIVATED = 0x8004,
        HSHELL_WINDOWREPLACED = 13, HSHELL_MONITORCHANGED = 16;

    public const uint MONITOR_DEFAULTTONEAREST = 2, MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MONITORINFOF_PRIMARY = 1;

    public const uint MIIM_STATE = 0x1, MIIM_ID = 0x2, MIIM_SUBMENU = 0x4, MIIM_TYPE = 0x10, MIIM_DATA = 0x20, MIIM_STRING = 0x40, MIIM_BITMAP = 0x80, MIIM_FTYPE = 0x100;
    public const uint MFT_SEPARATOR = 0x800, MFT_OWNERDRAW = 0x100, MFT_BITMAP = 0x4, MFT_RADIOCHECK = 0x200, MFT_MENUBARBREAK = 0x20;
    public const uint MFS_GRAYED = 0x3, MFS_DISABLED = 0x3, MFS_CHECKED = 0x8, MFS_HILITE = 0x80;

    public const uint SMTO_NORMAL = 0x0, SMTO_BLOCK = 0x1, SMTO_ABORTIFHUNG = 0x2, SMTO_NOTIMEOUTIFNOTHUNG = 0x8;

    public const uint KEYEVENTF_EXTENDEDKEY = 0x1, KEYEVENTF_KEYUP = 0x2;
    public const ushort VK_LWIN = 0x5B, VK_MENU = 0x12, VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_ESCAPE = 0x1B, VK_F1 = 0x70, VK_F4 = 0x73;

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, PROCESS_TERMINATE = 0x0001, SYNCHRONIZE = 0x00100000;

    public const uint FO_DELETE = 3;
    public const ushort FOF_ALLOWUNDO = 0x40, FOF_NOCONFIRMATION = 0x10, FOF_WANTNUKEWARNING = 0x4000;

    public const uint MSGFLT_ALLOW = 1;

    // ---- user32 ----
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int idx);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int idx, IntPtr val);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rc);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr hWnd, bool altTab);
    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(uint pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] public static extern bool SendNotifyMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] public static extern bool RegisterShellHookWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool DeregisterShellHookWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr hmod, WinEventDelegate cb, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFOEX mi);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetMenu(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetMenuItemCount(IntPtr hMenu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMenuItemInfo(IntPtr hMenu, uint item, bool byPos, ref MENUITEMINFO mii);
    [DllImport("user32.dll")] public static extern bool IsMenu(IntPtr hMenu);
    [DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint tid);
    [DllImport("user32.dll")] public static extern int GetKeyboardLayoutList(int n, [Out] IntPtr[]? list);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool LockWorkStation();
    [DllImport("user32.dll")] public static extern bool ExitWindowsEx(uint flags, uint reason);
    [DllImport("user32.dll")] public static extern IntPtr CopyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] public static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool UnregisterClass(string cls, IntPtr hInst);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateWindowEx(uint exStyle, string cls, string? name, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr hInst, IntPtr param);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int idx);
    [DllImport("user32.dll")] public static extern IntPtr GetLastActivePopup(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool FlashWindow(IntPtr hWnd, bool invert);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }
    [DllImport("user32.dll")] public static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] public static extern void PostQuitMessage(int code);

    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hMon, int type, out uint dpiX, out uint dpiY);

    // ---- kernel32 ----
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder sb, ref int size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern int GetApplicationUserModelId(IntPtr hProcess, ref int len, StringBuilder? sb);
    [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS st);
    [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX st);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr min, IntPtr max);
    [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] public static extern bool TerminateProcess(IntPtr h, uint code);

    // ---- shell32 ----
    [DllImport("shell32.dll")] public static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHFileOperation(ref SHFILEOPSTRUCT op);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHQueryRecycleBin(string? root, ref SHQUERYRBINFO info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? root, uint flags);
    [DllImport("shell32.dll")] public static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
    [DllImport("ole32.dll")] public static extern int PropVariantClear(ref PROPVARIANT pv);

    // ---- gdi32 ----
    [DllImport("gdi32.dll")] public static extern int GetObject(IntPtr h, int size, ref BITMAP bmp);
    [DllImport("gdi32.dll")] public static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER bi, uint usage);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    // ---- dwm ----
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);

    // ---- powrprof ----
    [DllImport("powrprof.dll")] public static extern bool SetSuspendState(bool hibernate, bool force, bool disableWake);

    // ---- psapi ----
    [DllImport("psapi.dll")] public static extern bool EmptyWorkingSet(IntPtr hProcess);

    // ---- accent (acrylic) ----
    [StructLayout(LayoutKind.Sequential)]
    public struct AccentPolicy { public int AccentState; public int AccentFlags; public uint GradientColor; public int AnimationId; }
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }
    [DllImport("user32.dll")] public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    // ---- helpers ----
    public static string GetWindowTitle(IntPtr h)
    {
        int len = GetWindowTextLength(h);
        if (len <= 0) return "";
        var sb = new StringBuilder(len + 2);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetClass(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    public static long GetExStyle(IntPtr h) => GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
    public static long GetStyle(IntPtr h) => GetWindowLongPtr(h, GWL_STYLE).ToInt64();

    public static void AddExStyle(IntPtr h, long add)
    {
        long ex = GetExStyle(h);
        SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(ex | add));
    }

    public static bool IsCloaked(IntPtr h)
    {
        try { return DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int v, 4) == 0 && v != 0; } catch { return false; }
    }

    public static string? GetProcessPath(uint pid)
    {
        IntPtr hp = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hp == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(hp, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally { CloseHandle(hp); }
    }

    public static string? GetProcessAumid(uint pid)
    {
        IntPtr hp = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hp == IntPtr.Zero) return null;
        try
        {
            int len = 0;
            GetApplicationUserModelId(hp, ref len, null);
            if (len <= 0 || len > 1024) return null;
            var sb = new StringBuilder(len);
            return GetApplicationUserModelId(hp, ref len, sb) == 0 ? sb.ToString() : null;
        }
        finally { CloseHandle(hp); }
    }

    static readonly Guid IID_IPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    static readonly PROPERTYKEY PKEY_AppUserModel_ID = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    public static string? GetWindowAumid(IntPtr hwnd)
    {
        try
        {
            var iid = IID_IPropertyStore;
            if (SHGetPropertyStoreForWindow(hwnd, ref iid, out IPropertyStore store) != 0 || store == null) return null;
            try
            {
                var key = PKEY_AppUserModel_ID;
                if (store.GetValue(ref key, out PROPVARIANT pv) != 0) return null;
                string? s = null;
                if (pv.vt == 31 && pv.pointerValue != IntPtr.Zero) s = Marshal.PtrToStringUni(pv.pointerValue);
                PropVariantClear(ref pv);
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            finally { Marshal.ReleaseComObject(store); }
        }
        catch { return null; }
    }

    public static IntPtr MakeLParam(int lo, int hi) => new((hi << 16) | (lo & 0xFFFF));

    public static void SendKeys(params ushort[] keysDownThenUp)
    {
        // Nhấn lần lượt rồi nhả theo thứ tự ngược lại
        var list = new List<INPUT>();
        foreach (var k in keysDownThenUp) list.Add(Key(k, false));
        for (int i = keysDownThenUp.Length - 1; i >= 0; i--) list.Add(Key(keysDownThenUp[i], true));
        SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf<INPUT>());
    }

    static INPUT Key(ushort vk, bool up)
    {
        uint flags = up ? KEYEVENTF_KEYUP : 0;
        if (vk == VK_LWIN) flags |= KEYEVENTF_EXTENDEDKEY;
        return new INPUT { type = 1, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = flags } } };
    }

    public static void TrimWorkingSet()
    {
        try { EmptyWorkingSet(GetCurrentProcess()); } catch { }
    }

    public static void SetAccent(IntPtr hwnd, int state, uint gradientAbgr)
    {
        var accent = new AccentPolicy { AccentState = state, AccentFlags = state == 4 ? 2 : 0, GradientColor = gradientAbgr };
        int size = Marshal.SizeOf(accent);
        IntPtr p = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, p, false);
            var data = new WindowCompositionAttributeData { Attribute = 19, Data = p, SizeOfData = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally { Marshal.FreeHGlobal(p); }
    }
}
