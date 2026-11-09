namespace MacDock;

public static class Paths
{
    static string? _root;

    /// <summary>Thư mục dữ liệu: %APPDATA%\MacDock (hoặc --config-dir khi kiểm thử).</summary>
    public static string Root
    {
        get
        {
            if (_root == null)
            {
                _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacDock");
                Directory.CreateDirectory(_root);
            }
            return _root;
        }
        set { _root = value; Directory.CreateDirectory(value); }
    }

    public static string ConfigFile => Path.Combine(Root, "config.json");
    public static string TaskbarStateFile => Path.Combine(Root, "taskbar-state.json");
    public static string LogFile => Path.Combine(Root, "macdock.log");
    public static string ThemesDir { get { var d = Path.Combine(Root, "themes"); Directory.CreateDirectory(d); return d; } }
    public static string IconsDir { get { var d = Path.Combine(Root, "icons"); Directory.CreateDirectory(d); return d; } }
    public static string ExePath => Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
}

public static class Diag
{
    public static readonly Dictionary<string, int> Sources = new();
    public static void Hit(string k) { lock (Sources) Sources[k] = Sources.GetValueOrDefault(k) + 1; }
    public static int Frames, Renders, Rescans, TrayChanges, MenuRebuilds, StatusChanges, ShellHooks, WinEvents;
    public static string Snapshot()
    {
        var s = $"frames={Frames} renders={Renders} rescans={Rescans} tray={TrayChanges} menuLeft={MenuRebuilds} status={StatusChanges} shellhook={ShellHooks} winevent={WinEvents}";
        lock (Sources) { s += " | " + string.Join(", ", Sources.Select(kv => kv.Key + "=" + kv.Value)); Sources.Clear(); }
        Frames = Renders = Rescans = TrayChanges = MenuRebuilds = StatusChanges = ShellHooks = WinEvents = 0;
        return s;
    }
}

public static class Log
{
    static readonly object _lock = new();
    public static bool Verbose { get; set; }

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", ex == null ? msg : msg + " :: " + ex);
    public static void Debug(string msg) { if (Verbose) Write("DEBUG", msg); }

    static void Write(string level, string msg)
    {
        try
        {
            lock (_lock)
            {
                var f = Paths.LogFile;
                var fi = new FileInfo(f);
                if (fi.Exists && fi.Length > 1_000_000)
                {
                    File.Copy(f, f + ".old", true);
                    File.WriteAllText(f, "");
                }
                File.AppendAllText(f, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
