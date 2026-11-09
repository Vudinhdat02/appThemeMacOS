using System.Diagnostics;
using System.Windows;

namespace MacDock;

public static class WindowActions
{
    public static void Activate(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            if (Native.IsIconic(hwnd)) Native.ShowWindowAsync(hwnd, Native.SW_RESTORE);
            IntPtr target = Native.GetLastActivePopup(hwnd);
            if (target == IntPtr.Zero || !Native.IsWindowVisible(target)) target = hwnd;
            if (Native.SetForegroundWindow(target) && Native.GetForegroundWindow() == target) return;

            IntPtr fg = Native.GetForegroundWindow();
            uint fgThread = Native.GetWindowThreadProcessId(fg, out _);
            uint me = Native.GetCurrentThreadId();
            bool attached = fgThread != 0 && fgThread != me && Native.AttachThreadInput(me, fgThread, true);
            try
            {
                Native.BringWindowToTop(target);
                Native.SetForegroundWindow(target);
            }
            finally { if (attached) Native.AttachThreadInput(me, fgThread, false); }
            if (Native.GetForegroundWindow() != target) Native.SwitchToThisWindow(target, true);
        }
        catch (Exception ex) { Log.Warn("Activate lỗi: " + ex.Message); }
    }

    public static void Minimize(IntPtr hwnd) => Native.PostMessage(hwnd, Native.WM_SYSCOMMAND, new IntPtr(0xF020), IntPtr.Zero); // SC_MINIMIZE
    public static void Maximize(IntPtr hwnd) => Native.PostMessage(hwnd, Native.WM_SYSCOMMAND, new IntPtr(0xF030), IntPtr.Zero); // SC_MAXIMIZE
    public static void RestoreWin(IntPtr hwnd) => Native.PostMessage(hwnd, Native.WM_SYSCOMMAND, new IntPtr(0xF120), IntPtr.Zero); // SC_RESTORE
    public static void Close(IntPtr hwnd) => Native.PostMessage(hwnd, Native.WM_SYSCOMMAND, new IntPtr(0xF060), IntPtr.Zero); // SC_CLOSE

    public static void KillProcess(uint pid)
    {
        try { Process.GetProcessById((int)pid).Kill(); } catch (Exception ex) { Log.Warn("Không buộc thoát được: " + ex.Message); }
    }

    /// <summary>Đưa cửa sổ lên trước rồi gửi tổ hợp phím (dùng cho menu chung).</summary>
    public static async void SendKeysTo(IntPtr hwnd, params ushort[] keys)
    {
        if (hwnd != IntPtr.Zero && Native.GetForegroundWindow() != hwnd) { Activate(hwnd); await Task.Delay(120); }
        Native.SendKeys(keys);
    }
}

public static class Launcher
{
    public static bool Launch(string kind, string path, string? args = null, string? file = null)
    {
        try
        {
            switch (kind)
            {
                case "aumid":
                    Process.Start(new ProcessStartInfo("explorer.exe", @"shell:AppsFolder\" + path) { UseShellExecute = true });
                    return true;
                case "folder":
                    Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
                    return true;
                default:
                    var psi = new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) ?? "" };
                    var a = args ?? "";
                    if (file != null) a = (a + " \"" + file + "\"").Trim();
                    if (a.Length > 0) psi.Arguments = a;
                    Process.Start(psi);
                    return true;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Không mở được {path}: {ex.Message}");
            return false;
        }
    }

    public static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn($"Không mở được {target}: {ex.Message}"); }
    }

    public static void ShowInExplorer(string path)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true }); } catch { }
    }
}

public static class PowerActions
{
    static bool Confirm(string text)
    {
        return MessageBox.Show(text, "MacDock", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public static void Sleep() { try { Native.SetSuspendState(false, false, false); } catch { } }
    public static void Lock() => Native.LockWorkStation();

    public static void Restart()
    {
        if (Confirm(L.T("Bạn có chắc muốn khởi động lại máy tính ngay bây giờ?")))
            Launcher.Launch("exe", Path.Combine(Environment.SystemDirectory, "shutdown.exe"), "/r /t 0");
    }

    public static void Shutdown()
    {
        if (Confirm(L.T("Bạn có chắc muốn tắt máy ngay bây giờ?")))
            Launcher.Launch("exe", Path.Combine(Environment.SystemDirectory, "shutdown.exe"), "/s /hybrid /t 0");
    }

    public static void LogOff()
    {
        if (Confirm(L.T("Đăng xuất khỏi Windows? Các ứng dụng đang mở sẽ được yêu cầu đóng.")))
            Native.ExitWindowsEx(0, 0);
    }
}

/// <summary>Bật/tắt tự khởi động bằng khoá HKCU\...\Run.</summary>
public static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "MacDock";

    public static bool IsEnabled
    {
        get
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(Name) is string;
        }
    }

    public static void Apply(bool enable)
    {
        if (Program.NoAutostartWrite) return;
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (enable)
            {
                string cmd = $"\"{Paths.ExePath}\" --autostart";
                if (Program.CustomConfigDir != null) cmd += $" --config-dir \"{Program.CustomConfigDir}\"";
                if (k.GetValue(Name) as string != cmd) k.SetValue(Name, cmd);
            }
            else if (k.GetValue(Name) != null) k.DeleteValue(Name, false);
        }
        catch (Exception ex) { Log.Warn("Autostart: " + ex.Message); }
    }
}
