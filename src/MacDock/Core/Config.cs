using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;

namespace MacDock;

public enum DockEdge { Bottom, Left, Right }
public enum ThemeMode { Light, Dark, System, Schedule }
public enum IconStyle { Original, Squircle }
public enum MonitorMode { All, Primary }

public class PinnedItem
{
    /// <summary>exe | aumid | folder</summary>
    public string Kind { get; set; } = "exe";
    public string Path { get; set; } = "";
    public string? Args { get; set; }
    public string? Name { get; set; }

    [JsonIgnore]
    public string Key => Kind == "aumid" ? "aumid:" + Path.ToLowerInvariant() : Kind == "folder" ? "folder:" + Path.ToLowerInvariant() : Path.ToLowerInvariant();
}

public class GeneralSettings
{
    public bool AutoStart { get; set; } = true;
    public bool HideTaskbar { get; set; } = true;
    public bool FirstRunDone { get; set; }
    public bool Watchdog { get; set; } = true;
    /// <summary>vi | en</summary>
    public string Language { get; set; } = "vi";
    /// <summary>macdock | windows — giao diện khi bấm phím âm lượng/độ sáng</summary>
    public string OsdStyle { get; set; } = "macdock";
    /// <summary>top-right | bottom</summary>
    public string OsdPosition { get; set; } = "top-right";
}

public class DockSettings
{
    public DockEdge Edge { get; set; } = DockEdge.Bottom;
    public double IconSize { get; set; } = 48;
    public bool Magnify { get; set; } = true;
    public double MagnifyScale { get; set; } = 1.75;
    public double CurveRange { get; set; } = 2.9;
    public double AnimSpeed { get; set; } = 18;
    public double Spacing { get; set; } = 6;
    public double EdgeMargin { get; set; } = 4;
    public bool ShowRunningDots { get; set; } = true;
    public bool ShowLabels { get; set; } = true;
    public bool Shadow { get; set; } = true;
    public bool BounceOnLaunch { get; set; } = true;
    public bool BounceOnAttention { get; set; } = true;
    public bool AutoHide { get; set; }
    public int AutoHideShowDelayMs { get; set; } = 0;
    public bool AutoHideOnlyWhenCovered { get; set; } = true;
    public int AutoHideHideDelayMs { get; set; } = 500;
    public bool ReserveSpace { get; set; } = true;
    public bool ShowRunningUnpinned { get; set; } = true;
    public bool ShowLaunchpad { get; set; } = true;
    /// <summary>grid | windows | custom</summary>
    public string LaunchpadIcon { get; set; } = "grid";
    public string? LaunchpadImage { get; set; }
    public bool ShowTrash { get; set; } = true;
    public bool ShowBadges { get; set; } = true;
    public bool WindowPreviews { get; set; } = true;
    public int PreviewDelayMs { get; set; } = 450;
    public double PreviewWidth { get; set; } = 200;
    public bool BadgeFromTitle { get; set; } = true;
    public MonitorMode Monitors { get; set; } = MonitorMode.All;
    public List<PinnedItem> Pinned { get; set; } = new();
}

public class MenuBarSettings
{
    public bool Enabled { get; set; } = true;
    public double Height { get; set; } = 26;
    public string ClockFormat { get; set; } = "ddd d 'thg' M   HH:mm";
    public bool ShowBattery { get; set; } = true;
    public bool ShowBatteryPercent { get; set; } = true;
    public bool ShowWifi { get; set; } = true;
    public bool ShowVolume { get; set; } = true;
    public bool ShowInputLang { get; set; } = true;
    public bool ShowControlCenter { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool ShowTray { get; set; } = true;
    public bool ShowAppMenus { get; set; } = true;
    public bool UseUIAutomation { get; set; } = true;
    public bool BoldAppName { get; set; } = true;
    /// <summary>transparent (macOS 26) | glass | dock | solid | theme</summary>
    public string Style { get; set; } = "liquid";
    /// <summary>0..1: độ mờ/bóng của kính liquid</summary>
    public double Liquid { get; set; } = 0.55;
    public double Opacity { get; set; } = 1.0;
    /// <summary>auto | light | dark</summary>
    public string TextColor { get; set; } = "auto";
    /// <summary>circle | flower | custom</summary>
    public string LogoStyle { get; set; } = "circle";
    public string? LogoImage { get; set; }
    public MonitorMode Monitors { get; set; } = MonitorMode.All;
    public List<string> HiddenTrayIcons { get; set; } = new() { "explorer.exe|1225" };
    /// <summary>Các ô của Trung tâm điều khiển theo thứ tự; nút nhỏ có hậu tố ":s" (tròn) hoặc ":m" (viên thuốc).</summary>
    public List<string> ControlCenter { get; set; } = new() { "connectivity", "nowplaying", "focus:m", "darkmode:s", "screenshot:s", "display", "sound" };
}

public class ThemeSettings
{
    public string ThemeId { get; set; } = "sonoma";
    public ThemeMode Mode { get; set; } = ThemeMode.System;
    public string LightStart { get; set; } = "06:30";
    public string DarkStart { get; set; } = "18:30";
    public IconStyle IconStyle { get; set; } = IconStyle.Original;
    public string? IconPack { get; set; }
    public Dictionary<string, string> IconOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Tuỳ chỉnh đè lên theme
    public double? CornerRadius { get; set; }
    public double? Opacity { get; set; }
    public bool? Border { get; set; }
    public string? FontFamily { get; set; }
    public string? MenuBarMaterial { get; set; }
}

public class PerformanceSettings
{
    public int FpsLimit { get; set; } = 60;
    public bool DisableAnimations { get; set; }
    public bool ReduceOnBattery { get; set; } = true;
    public bool TrimMemory { get; set; } = true;
}

public class AppConfig
{
    public int Version { get; set; } = 3;
    public GeneralSettings General { get; set; } = new();
    public DockSettings Dock { get; set; } = new();
    public MenuBarSettings MenuBar { get; set; } = new();
    public ThemeSettings Theme { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
}

public static class Config
{
    public static AppConfig Current { get; private set; } = new();

    /// <summary>Phát ra khi cấu hình thay đổi (tên mục: general/dock/menubar/theme/performance/all).</summary>
    public static event Action<string>? Changed;

    static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
    public static JsonSerializerOptions JsonOptions => _json;

    static DispatcherTimer? _saveTimer;
    static readonly HashSet<string> _pending = new();
    static DispatcherTimer? _notifyTimer;

    public static bool IsFirstRun { get; private set; }

    public static void Load()
    {
        try
        {
            if (File.Exists(Paths.ConfigFile))
            {
                string text = File.ReadAllText(Paths.ConfigFile);
                var migrated = TryMigrateV01(text);
                if (migrated != null)
                {
                    try { File.Copy(Paths.ConfigFile, Path.Combine(Paths.Root, "config.v01.json"), true); } catch { }
                    Current = migrated;
                    IsFirstRun = true;
                    Log.Info("Đã chuyển cấu hình MacDock 0.1 sang 0.2 (bản cũ: config.v01.json)");
                }
                else
                {
                    var c = JsonSerializer.Deserialize<AppConfig>(text, _json);
                    if (c != null) Current = c;
                }
            }
            else IsFirstRun = true;
        }
        catch (Exception ex)
        {
            Log.Error("Không đọc được config.json, dùng mặc định", ex);
            try { File.Copy(Paths.ConfigFile, Paths.ConfigFile + ".broken", true); } catch { }
            Current = new AppConfig();
        }
        Sanitize(Current);
        if (!Current.General.FirstRunDone) IsFirstRun = true;
        if (Current.Version < 3)
        {
            // 0.3: Dock hiện ngay khi chạm cạnh màn hình
            if (Current.Dock.AutoHideShowDelayMs is 250 or 80) Current.Dock.AutoHideShowDelayMs = 0;
            if (Current.MenuBar.Style is "transparent" or "theme") Current.MenuBar.Style = "liquid";
            Current.Version = 3;
            if (File.Exists(Paths.ConfigFile)) Save();
        }
        if (Current.Dock.Pinned.Count == 0 && IsFirstRun) Current.Dock.Pinned = DefaultPins();
    }

    /// <summary>Cấu hình 0.1 là JSON phẳng (IconSize, Magnification, Pinned[{Name, Path, Arguments}]...).</summary>
    static AppConfig? TryMigrateV01(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object || r.TryGetProperty("Version", out _) || !r.TryGetProperty("IconSize", out _)) return null;
            var c = new AppConfig();
            double D(string n, double d) => r.TryGetProperty(n, out var v) && v.TryGetDouble(out var x) ? x : d;
            bool B(string n, bool d) => r.TryGetProperty(n, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : d;
            string? S(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            c.Dock.IconSize = D("IconSize", c.Dock.IconSize);
            double mag = D("Magnification", c.Dock.MagnifyScale);
            c.Dock.Magnify = mag > 1.01; c.Dock.MagnifyScale = Math.Max(1.1, mag);
            c.Dock.AutoHide = B("AutoHide", false);
            c.Dock.ReserveSpace = B("ReserveSpace", true);
            c.Dock.ShowLaunchpad = B("ShowLaunchpad", true);
            c.Dock.ShowTrash = B("ShowTrash", true);
            c.Dock.BounceOnLaunch = B("BounceOnLaunch", true);
            c.General.HideTaskbar = B("HideWindowsTaskbar", true);
            c.Theme.Mode = (S("Theme") ?? "auto").ToLowerInvariant() switch { "light" => ThemeMode.Light, "dark" => ThemeMode.Dark, _ => ThemeMode.System };
            if ((S("IconStyle") ?? "").Equals("tile", StringComparison.OrdinalIgnoreCase)) c.Theme.IconStyle = IconStyle.Squircle;
            if (r.TryGetProperty("Pinned", out var pins) && pins.ValueKind == JsonValueKind.Array)
                foreach (var p in pins.EnumerateArray())
                {
                    string? path = p.TryGetProperty("Path", out var pv) ? pv.GetString() : null;
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    string? name = p.TryGetProperty("Name", out var nv) ? nv.GetString() : null;
                    string? args = p.TryGetProperty("Arguments", out var av) && av.ValueKind == JsonValueKind.String ? av.GetString() : null;
                    const string apps = @"shell:AppsFolder\";
                    if (path.StartsWith(apps, StringComparison.OrdinalIgnoreCase))
                        c.Dock.Pinned.Add(new PinnedItem { Kind = "aumid", Path = path.Substring(apps.Length), Name = name });
                    else if (Directory.Exists(path))
                        c.Dock.Pinned.Add(new PinnedItem { Kind = "folder", Path = path, Name = name });
                    else
                        c.Dock.Pinned.Add(new PinnedItem { Kind = "exe", Path = path, Name = name, Args = args });
                }
            return c;
        }
        catch { return null; }
    }

    static void Sanitize(AppConfig c)
    {
        c.General ??= new(); c.Dock ??= new(); c.MenuBar ??= new(); c.Theme ??= new(); c.Performance ??= new();
        c.Dock.Pinned ??= new();
        c.Theme.IconOverrides = new Dictionary<string, string>(c.Theme.IconOverrides ?? new(), StringComparer.OrdinalIgnoreCase);
        c.MenuBar.HiddenTrayIcons ??= new();
        c.Dock.IconSize = Math.Clamp(c.Dock.IconSize, 20, 128);
        c.Dock.MagnifyScale = Math.Clamp(c.Dock.MagnifyScale, 1, 3);
        c.Dock.CurveRange = Math.Clamp(c.Dock.CurveRange, 1, 6);
        c.Dock.AnimSpeed = Math.Clamp(c.Dock.AnimSpeed, 2, 60);
        c.MenuBar.Height = Math.Clamp(c.MenuBar.Height, 20, 40);
        if (c.Performance.FpsLimit < 0) c.Performance.FpsLimit = 60;
        if (string.IsNullOrWhiteSpace(c.MenuBar.ClockFormat)) c.MenuBar.ClockFormat = new MenuBarSettings().ClockFormat;
    }

    public static void Save()
    {
        try
        {
            var tmp = Paths.ConfigFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, _json));
            File.Move(tmp, Paths.ConfigFile, true);
        }
        catch (Exception ex) { Log.Error("Lưu cấu hình lỗi", ex); }
    }

    /// <summary>Báo thay đổi: áp dụng ngay (gộp trong 30ms) và lưu sau 600ms.</summary>
    public static void Touch(string section)
    {
        lock (_pending) _pending.Add(section);
        if (_notifyTimer == null)
        {
            _notifyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _notifyTimer.Tick += (_, _) =>
            {
                _notifyTimer!.Stop();
                string[] list;
                lock (_pending) { list = _pending.ToArray(); _pending.Clear(); }
                foreach (var s in list)
                {
                    try { Changed?.Invoke(s); } catch (Exception ex) { Log.Error("Áp dụng cấu hình lỗi", ex); }
                }
            };
        }
        _notifyTimer.Stop(); _notifyTimer.Start();

        if (_saveTimer == null)
        {
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _saveTimer.Tick += (_, _) => { _saveTimer!.Stop(); Save(); };
        }
        _saveTimer.Stop(); _saveTimer.Start();
    }

    public static void ResetSection(string section)
    {
        var d = new AppConfig();
        switch (section)
        {
            case "general": d.General.FirstRunDone = true; Current.General = d.General; break;
            case "dock": d.Dock.Pinned = Current.Dock.Pinned; Current.Dock = d.Dock; break;
            case "menubar": Current.MenuBar = d.MenuBar; break;
            case "theme": d.Theme.IconOverrides = Current.Theme.IconOverrides; Current.Theme = d.Theme; break;
            case "performance": Current.Performance = d.Performance; break;
            case "all":
                d.General.FirstRunDone = true;
                d.Dock.Pinned = Current.Dock.Pinned;
                Current = d;
                break;
        }
        Touch(section == "all" ? "all" : section);
    }

    public static List<PinnedItem> DefaultPins()
    {
        var list = new List<PinnedItem>();
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        void AddExe(string path, string? name = null)
        {
            if (File.Exists(path) && !list.Any(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                list.Add(new PinnedItem { Kind = "exe", Path = path, Name = name });
        }
        AddExe(Path.Combine(win, "explorer.exe"), "File Explorer");
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        AddExe(Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"), "Microsoft Edge");
        AddExe(Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"), "Google Chrome");
        AddExe(Path.Combine(local, @"Google\Chrome\Application\chrome.exe"), "Google Chrome");
        AddExe(Path.Combine(local, @"Programs\Zalo\Zalo.exe"), "Zalo");
        AddExe(Path.Combine(local, @"Programs\Microsoft VS Code\Code.exe"), "Visual Studio Code");
        void AddAumid(string aumid, string name)
        {
            if (AppIdentity.AumidExists(aumid)) list.Add(new PinnedItem { Kind = "aumid", Path = aumid, Name = name });
        }
        AddAumid("windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel", "Cài đặt");
        AddAumid("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "Máy tính");
        if (AppIdentity.AumidExists("Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"))
            list.Add(new PinnedItem { Kind = "aumid", Path = "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App", Name = "Notepad" });
        else AddExe(Path.Combine(win, @"System32\notepad.exe"), "Notepad");
        AddAumid("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", "Terminal");
        return list;
    }
}
