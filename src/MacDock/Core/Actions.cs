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
        return ConfirmDialog.Ask(text, "", L.T("Đồng ý"));
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

/// <summary>
/// Tự khởi động: dùng tác vụ Task Scheduler "khi đăng nhập" (chạy ngay, không bị Windows trì hoãn như khoá Run).
/// Nếu không tạo được tác vụ thì dùng khoá HKCU\...\Run.
/// </summary>
public static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "MacDock";
    static string TaskName => "MacDock Autostart (" + Environment.UserName + ")";

    public static bool IsEnabled
    {
        get
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(Name) is string || TaskExists();
        }
    }

    static string Args()
    {
        string a = "--autostart";
        if (Program.CustomConfigDir != null) a += $" --config-dir \"{Program.CustomConfigDir}\"";
        return a;
    }

    static int Schtasks(string args, out string output)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), args)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = System.Diagnostics.Process.Start(psi)!;
        output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(10000);
        return p.ExitCode;
    }

    static bool TaskExists()
    {
        try { return Schtasks($"/query /tn \"{TaskName}\" /xml", out _) == 0; } catch { return false; }
    }

    static string? CurrentTaskCommand()
    {
        try
        {
            if (Schtasks($"/query /tn \"{TaskName}\" /xml", out string xml) != 0) return null;
            var m = System.Text.RegularExpressions.Regex.Match(xml, "<Command>(.*?)</Command>.*?<Arguments>(.*?)</Arguments>", System.Text.RegularExpressions.RegexOptions.Singleline);
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) + "|" + System.Net.WebUtility.HtmlDecode(m.Groups[2].Value) : "";
        }
        catch { return null; }
    }

    static bool CreateTask()
    {
        string user = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        string Esc(string s) => System.Security.SecurityElement.Escape(s) ?? s;
        string xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo><Description>Start MacDock when you sign in</Description></RegistrationInfo>
  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{Esc(user)}</UserId></LogonTrigger></Triggers>
  <Principals><Principal id=""Author""><UserId>{Esc(user)}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>false</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>4</Priority>
  </Settings>
  <Actions Context=""Author""><Exec><Command>{Esc(Paths.ExePath)}</Command><Arguments>{Esc(Args())}</Arguments></Exec></Actions>
</Task>";
        string f = Path.Combine(Path.GetTempPath(), "macdock-task.xml");
        try
        {
            File.WriteAllText(f, xml, System.Text.Encoding.Unicode);
            int rc = Schtasks($"/create /tn \"{TaskName}\" /xml \"{f}\" /f", out string outp);
            if (rc != 0) Log.Warn("Không tạo được tác vụ tự khởi động: " + outp.Trim());
            return rc == 0;
        }
        finally { try { File.Delete(f); } catch { } }
    }

    public static void Apply(bool enable)
    {
        if (Program.NoAutostartWrite) return;
        Task.Run(() =>
        {
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey, true);
                if (enable)
                {
                    string want = Paths.ExePath + "|" + Args();
                    bool ok = CurrentTaskCommand() == want || CreateTask();
                    if (ok)
                    {
                        if (k.GetValue(Name) != null) k.DeleteValue(Name, false); // tránh chạy đôi
                    }
                    else
                    {
                        string cmd = $"\"{Paths.ExePath}\" {Args()}";
                        if (k.GetValue(Name) as string != cmd) k.SetValue(Name, cmd);
                    }
                }
                else
                {
                    if (k.GetValue(Name) != null) k.DeleteValue(Name, false);
                    if (TaskExists()) Schtasks($"/delete /tn \"{TaskName}\" /f", out _);
                }
            }
            catch (Exception ex) { Log.Warn("Autostart: " + ex.Message); }
        });
    }
}