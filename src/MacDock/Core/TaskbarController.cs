using System.Runtime.InteropServices;
using System.Text.Json;

namespace MacDock;

/// <summary>
/// Ẩn / khôi phục taskbar gốc của Windows. Trạng thái gốc được ghi ra file trước khi ẩn
/// để có thể khôi phục sau khi crash (lần chạy kế tiếp, tiến trình watchdog hoặc --restore-taskbar).
/// </summary>
public static class TaskbarController
{
    sealed class SavedState
    {
        public int OriginalState { get; set; } = Native.ABS_ALWAYSONTOP;
        public bool Hidden { get; set; }
        public int Pid { get; set; }
        public DateTime Time { get; set; }
    }

    static SavedState? _state;
    static readonly object _lock = new();
    public static bool IsHidden => _state?.Hidden == true;

    static IEnumerable<IntPtr> TrayWindows(bool excludeOwn = true)
    {
        uint own = (uint)Environment.ProcessId;
        foreach (var cls in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
        {
            IntPtr h = IntPtr.Zero;
            while ((h = Native.FindWindowEx(IntPtr.Zero, h, cls, null)) != IntPtr.Zero)
            {
                Native.GetWindowThreadProcessId(h, out uint pid);
                if (excludeOwn && pid == own) continue;
                yield return h;
            }
        }
    }

    public static IntPtr ExplorerTray()
    {
        foreach (var h in TrayWindows()) if (Native.GetClass(h) == "Shell_TrayWnd") return h;
        return IntPtr.Zero;
    }

    static int GetState()
    {
        var abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
        return (int)Native.SHAppBarMessage(Native.ABM_GETSTATE, ref abd).ToInt64();
    }

    /// <summary>
    /// Cờ tự ẩn người dùng đã lưu nằm trong StuckRects3 (byte 8, bit 0). Nếu một app khác đang tạm bật tự ẩn
    /// thì ABM_GETSTATE không phản ánh đúng cài đặt thật, nên ưu tiên registry.
    /// </summary>
    static int OriginalFromRegistry(int current)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3");
            if (k?.GetValue("Settings") is byte[] b && b.Length > 8)
                return (b[8] & 1) != 0 ? current | Native.ABS_AUTOHIDE : current & ~Native.ABS_AUTOHIDE;
        }
        catch { }
        return current;
    }

    static void SetState(int state)
    {
        var abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = ExplorerTray(), lParam = new IntPtr(state) };
        Native.SHAppBarMessage(Native.ABM_SETSTATE, ref abd);
    }

    static IntPtr _showHook;
    static uint _hookPid;
    static WinEventDelegate? _showProc;

    /// <summary>Theo dõi sự kiện hiện cửa sổ của riêng tiến trình Explorer: taskbar vừa hiện là ẩn lại ngay.</summary>
    static void EnsureShowHook()
    {
        try
        {
            var tray = ExplorerTray();
            if (tray == IntPtr.Zero) return;
            Native.GetWindowThreadProcessId(tray, out uint pid);
            if (pid == _hookPid && _showHook != IntPtr.Zero) return;
            RemoveShowHook();
            _showProc ??= (hook, ev, hwnd, idObject, idChild, thread, time) =>
            {
                if (idObject != Native.OBJID_WINDOW || _state?.Hidden != true) return;
                string cls = Native.GetClass(hwnd);
                if (cls is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") Native.ShowWindow(hwnd, Native.SW_HIDE);
            };
            _showHook = Native.SetWinEventHook(Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW, IntPtr.Zero, _showProc, pid, 0, Native.WINEVENT_OUTOFCONTEXT);
            _hookPid = pid;
        }
        catch (Exception ex) { Log.Warn("Hook taskbar: " + ex.Message); }
    }

    public static void PrepareRestoreOnUiThread() => RemoveShowHook();

    static void RemoveShowHook()
    {
        if (_showHook != IntPtr.Zero) { Native.UnhookWinEvent(_showHook); _showHook = IntPtr.Zero; _hookPid = 0; }
    }

    public static void Hide()
    {
        lock (_lock)
        {
            try
            {
                if (_state?.Hidden != true)
                {
                    // Nếu file cũ còn (crash trước đó) thì dùng trạng thái gốc trong file
                    var old = ReadFile();
                    int original = old?.Hidden == true ? old.OriginalState : OriginalFromRegistry(GetState());
                    _state = new SavedState { OriginalState = original, Hidden = true, Pid = Environment.ProcessId, Time = DateTime.Now };
                    WriteFile(_state);
                    if ((original & Native.ABS_AUTOHIDE) == 0) SetState(original | Native.ABS_AUTOHIDE);
                    Log.Info($"Ẩn taskbar (trạng thái gốc = {original})");
                }
                foreach (var h in TrayWindows())
                    if (Native.IsWindowVisible(h)) Native.ShowWindow(h, Native.SW_HIDE);
                EnsureShowHook();
            }
            catch (Exception ex) { Log.Error("Ẩn taskbar lỗi", ex); }
        }
    }

    /// <summary>Gọi định kỳ / khi Explorer khởi động lại để đảm bảo taskbar vẫn ẩn.</summary>
    public static void EnsureHidden()
    {
        if (_state?.Hidden != true) return;
        try
        {
            if ((GetState() & Native.ABS_AUTOHIDE) == 0) SetState(_state.OriginalState | Native.ABS_AUTOHIDE);
            foreach (var h in TrayWindows())
                if (Native.IsWindowVisible(h)) Native.ShowWindow(h, Native.SW_HIDE);
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true) EnsureShowHook();
        }
        catch { }
    }

    public static void Restore()
    {
        lock (_lock)
        {
            try
            {
                var s = _state ?? ReadFile();
                if (s == null || !s.Hidden) return;
                if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true) RemoveShowHook();
                SetState(s.OriginalState);
                foreach (var h in TrayWindows()) Native.ShowWindow(h, Native.SW_SHOWNA);
                _state = null;
                DeleteFile();
                Log.Info($"Đã khôi phục taskbar (trạng thái = {s.OriginalState})");
            }
            catch (Exception ex) { Log.Error("Khôi phục taskbar lỗi", ex); }
        }
    }

    /// <summary>Khôi phục từ file khi tiến trình trước bị tắt đột ngột.</summary>
    public static bool RecoverFromCrash(bool force = false)
    {
        var s = ReadFile();
        if (s == null || !s.Hidden) return false;
        if (!force && s.Pid != 0 && s.Pid != Environment.ProcessId)
        {
            try
            {
                var p = System.Diagnostics.Process.GetProcessById(s.Pid);
                if (!p.HasExited && p.ProcessName.Equals("MacDock", StringComparison.OrdinalIgnoreCase)) return false; // vẫn đang chạy
            }
            catch { }
        }
        _state = s;
        Restore();
        return true;
    }

    /// <summary>Hiện taskbar mà không cần file trạng thái (dùng cho --restore-taskbar khi file bị mất).</summary>
    public static void ForceShow()
    {
        try
        {
            foreach (var h in TrayWindows()) Native.ShowWindow(h, Native.SW_SHOWNA);
        }
        catch { }
    }

    static SavedState? ReadFile()
    {
        try
        {
            if (!File.Exists(Paths.TaskbarStateFile)) return null;
            return JsonSerializer.Deserialize<SavedState>(File.ReadAllText(Paths.TaskbarStateFile));
        }
        catch { return null; }
    }

    static void WriteFile(SavedState s)
    {
        try { File.WriteAllText(Paths.TaskbarStateFile, JsonSerializer.Serialize(s)); } catch (Exception ex) { Log.Error("Ghi trạng thái taskbar lỗi", ex); }
    }

    static void DeleteFile()
    {
        try { if (File.Exists(Paths.TaskbarStateFile)) File.Delete(Paths.TaskbarStateFile); } catch { }
    }

    /// <summary>Tiến trình canh chừng: chờ tiến trình chính thoát, nếu taskbar vẫn bị ẩn thì khôi phục.</summary>
    public static int RunWatchdog(int pid)
    {
        try
        {
            Native.TrimWorkingSet();
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            p.WaitForExit();
        }
        catch { }
        Thread.Sleep(500);
        var s = ReadFile();
        if (s != null && s.Hidden && s.Pid == pid)
        {
            _state = s;
            Restore();
            Log.Warn($"Watchdog: tiến trình {pid} đã thoát bất thường, đã khôi phục taskbar.");
        }
        return 0;
    }
}
