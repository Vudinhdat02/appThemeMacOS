using System.Diagnostics;

namespace MacDock;

public static class Program
{
    public static string? CustomConfigDir { get; private set; }
    public static bool LaunchedByAutostart { get; private set; }
    public static bool OpenSettingsOnStart { get; private set; }
    public static bool NoAutostartWrite { get; private set; }
    public static bool TestMode { get; private set; }
    public const string MessagingTitle = "MacDock.Messaging";

    [STAThread]
    public static int Main(string[] args)
    {
        int? watchdogPid = null;
        bool restore = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--config-dir" when i + 1 < args.Length: CustomConfigDir = Path.GetFullPath(args[++i]); break;
                case "--restore-taskbar": restore = true; break;
                case "--watchdog" when i + 1 < args.Length: if (int.TryParse(args[++i], out int p)) watchdogPid = p; break;
                case "--autostart": LaunchedByAutostart = true; break;
                case "--settings": OpenSettingsOnStart = true; break;
                case "--verbose": Log.Verbose = true; break;
                case "--no-autostart": NoAutostartWrite = true; break;
                case "--test": TestMode = true; NoAutostartWrite = true; break;
                case "--after" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out int prev))
                        try { System.Diagnostics.Process.GetProcessById(prev).WaitForExit(15000); } catch { }
                    break;
            }
        }
        if (CustomConfigDir != null) Paths.Root = CustomConfigDir;

        if (watchdogPid != null) return TaskbarController.RunWatchdog(watchdogPid.Value);

        if (restore)
        {
            // Báo tiến trình đang chạy (nếu có) thoát, rồi khôi phục taskbar
            IntPtr other = Native.FindWindow(null, MessagingTitle);
            if (other != IntPtr.Zero)
            {
                Native.PostMessage(other, Native.RegisterWindowMessage("MacDock.Quit"), IntPtr.Zero, IntPtr.Zero);
                for (int i = 0; i < 30 && Native.IsWindow(other); i++) Thread.Sleep(100);
            }
            if (!TaskbarController.RecoverFromCrash(force: true)) TaskbarController.ForceShow();
            Console.WriteLine("Đã khôi phục taskbar.");
            return 0;
        }

        string mutexName = @"Local\MacDock.SingleInstance" + (CustomConfigDir != null ? "." + CustomConfigDir.GetHashCode().ToString("X") : "");
        using var mutex = new Mutex(true, mutexName, out bool created);
        if (!created)
        {
            // Đã có MacDock đang chạy: yêu cầu nó mở cửa sổ Cài đặt
            IntPtr other = Native.FindWindow(null, MessagingTitle);
            if (other != IntPtr.Zero)
            {
                Native.GetWindowThreadProcessId(other, out uint pid);
                Native.AllowSetForegroundWindow(pid);
                if (!LaunchedByAutostart)
                    Native.PostMessage(other, Native.RegisterWindowMessage("MacDock.OpenSettings"), IntPtr.Zero, IntPtr.Zero);
            }
            return 0;
        }

        return RunApp();
    }

    // Tách riêng để tiến trình watchdog không phải nạp WPF
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int RunApp()
    {
        try
        {
            var app = new App();
            app.Run();
            if (App.RestartRequested)
            {
                var args = new List<string>();
                var src = Environment.GetCommandLineArgs().Skip(1).ToList();
                for (int k = 0; k < src.Count; k++)
                {
                    if (src[k] == "--after") { k++; continue; }
                    args.Add(src[k].Contains(' ') ? "\"" + src[k] + "\"" : src[k]);
                }
                args.Add("--after");
                args.Add(Environment.ProcessId.ToString());
                Process.Start(new ProcessStartInfo(Paths.ExePath, string.Join(" ", args)) { UseShellExecute = false });
            }
        }
        catch (Exception ex)
        {
            Log.Error("Lỗi nghiêm trọng", ex);
            App.EmergencyCleanup();
            return 1;
        }
        return 0;
    }

    /// <summary>Chạy tiến trình watchdog nhỏ để khôi phục taskbar nếu MacDock bị tắt đột ngột.</summary>
    public static void StartWatchdog()
    {
        try
        {
            var args = $"--watchdog {Environment.ProcessId}";
            if (CustomConfigDir != null) args += $" --config-dir \"{CustomConfigDir}\"";
            Process.Start(new ProcessStartInfo(Paths.ExePath, args) { UseShellExecute = false, CreateNoWindow = true });
        }
        catch (Exception ex) { Log.Warn("Không chạy được watchdog: " + ex.Message); }
    }
}
