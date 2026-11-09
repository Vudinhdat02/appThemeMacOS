using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace MacDock;

public class ThemePalette
{
    public string DockBackground { get; set; } = "#66F6F6F8";
    public string? DockBackground2 { get; set; }
    public string DockBorder { get; set; } = "#66FFFFFF";
    public string DockHighlight { get; set; } = "#40FFFFFF";
    public string Dot { get; set; } = "#CC1D1D1F";
    public string LabelBackground { get; set; } = "#E6F2F2F4";
    public string LabelForeground { get; set; } = "#FF1D1D1F";
    public string MenuBarBackground { get; set; } = "#B3F4F4F6";
    public string? MenuBarBackground2 { get; set; }
    public string MenuBarForeground { get; set; } = "#FF111111";
    public string MenuHighlight { get; set; } = "#26000000";
    public string PopupBackground { get; set; } = "#F2F5F5F7";
    public string PopupForeground { get; set; } = "#FF1D1D1F";
    public string PopupBorder { get; set; } = "#33000000";
    public string Selection { get; set; } = "#FF0A64D8";
    public string SelectionForeground { get; set; } = "#FFFFFFFF";
    public string Accent { get; set; } = "#FF0A84FF";
    public string IconPlate { get; set; } = "#FFFFFFFF";
    public string? IconPlate2 { get; set; } = "#FFE9E9EE";
    public string Separator { get; set; } = "#33000000";
    public string Badge { get; set; } = "#FFFF3B30";
    public string WindowBackground { get; set; } = "#FFF0F0F3";
    public string CardBackground { get; set; } = "#FFFFFFFF";
}

public class ThemeDefinition
{
    public string Id { get; set; } = "custom";
    public string Name { get; set; } = "Tuỳ chỉnh";
    public string Author { get; set; } = "";
    public int Version { get; set; } = 1;
    public ThemePalette Light { get; set; } = new();
    public ThemePalette Dark { get; set; } = new();
    public double CornerRadius { get; set; } = 18;
    public double PopupCornerRadius { get; set; } = 9;
    public double BorderThickness { get; set; } = 1;
    public double ShadowBlur { get; set; } = 22;
    public double ShadowOpacity { get; set; } = 0.28;
    /// <summary>acrylic | blur | solid</summary>
    public string MenuBarMaterial { get; set; } = "acrylic";
    /// <summary>glass | solid</summary>
    public string DockMaterial { get; set; } = "glass";
    public string FontFamily { get; set; } = "Segoe UI Variable Text, Segoe UI";
    public double FontSize { get; set; } = 13;
    public string? IconStyle { get; set; }

    [JsonIgnore] public bool BuiltIn { get; set; }
    [JsonIgnore] public string? Folder { get; set; }

    public ThemeDefinition Clone() => JsonSerializer.Deserialize<ThemeDefinition>(JsonSerializer.Serialize(this, Config.JsonOptions), Config.JsonOptions)!;
}

/// <summary>Bảng màu đã phân giải (đã chọn sáng/tối + áp tuỳ chỉnh) dưới dạng brush đã Freeze.</summary>
public sealed class ResolvedTheme
{
    public ThemeDefinition Def { get; init; } = null!;
    public bool IsDark { get; init; }
    public Brush DockBackground = null!, DockBorder = null!, DockHighlight = null!, Dot = null!, LabelBackground = null!, LabelForeground = null!,
        MenuBarBackground = null!, MenuBarForeground = null!, MenuHighlight = null!, PopupBackground = null!, PopupForeground = null!, PopupBorder = null!,
        Selection = null!, SelectionForeground = null!, Accent = null!, IconPlate = null!, Separator = null!, Badge = null!, WindowBackground = null!, CardBackground = null!;
    public Color MenuBarColor, AccentColor;
    public double CornerRadius, PopupCornerRadius, BorderThickness, ShadowBlur, ShadowOpacity;
    public string MenuBarMaterial = "acrylic";
    public FontFamily Font = new("Segoe UI");
    public double FontSize = 13;
    public IconStyle IconStyle;
}

public static class ThemeManager
{
    public static List<ThemeDefinition> BuiltIns { get; } = CreateBuiltIns();
    public static ResolvedTheme Current { get; private set; } = null!;
    public static event Action? Changed;

    public static IEnumerable<ThemeDefinition> All()
    {
        foreach (var t in BuiltIns) yield return t;
        foreach (var t in LoadUserThemes()) yield return t;
    }

    public static ThemeDefinition Find(string id) =>
        All().FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? BuiltIns[0];

    static List<ThemeDefinition> LoadUserThemes()
    {
        var list = new List<ThemeDefinition>();
        try
        {
            foreach (var dir in Directory.GetDirectories(Paths.ThemesDir))
            {
                var f = Path.Combine(dir, "theme.json");
                if (!File.Exists(f)) continue;
                try
                {
                    var t = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(f), Config.JsonOptions);
                    if (t == null) continue;
                    t.Folder = dir;
                    if (string.IsNullOrWhiteSpace(t.Id)) t.Id = Path.GetFileName(dir);
                    list.Add(t);
                }
                catch (Exception ex) { Log.Warn($"Theme lỗi {f}: {ex.Message}"); }
            }
        }
        catch { }
        return list;
    }

    public static bool SystemIsDark()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    static bool ScheduleIsDark(ThemeSettings s)
    {
        TimeSpan.TryParse(s.LightStart, out var light);
        TimeSpan.TryParse(s.DarkStart, out var dark);
        if (light == default && dark == default) { light = new TimeSpan(6, 30, 0); dark = new TimeSpan(18, 30, 0); }
        var now = DateTime.Now.TimeOfDay;
        return light < dark ? !(now >= light && now < dark) : (now >= dark && now < light);
    }

    public static bool WantDark()
    {
        var s = Config.Current.Theme;
        return s.Mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            ThemeMode.Schedule => ScheduleIsDark(s),
            _ => SystemIsDark()
        };
    }

    static Color C(string? s, string fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(s) ? fallback : s); }
        catch { return (Color)ColorConverter.ConvertFromString(fallback); }
    }

    static Brush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    static Color Fade(Color c, double mul) => Color.FromArgb((byte)Math.Clamp(c.A * mul, 0, 255), c.R, c.G, c.B);

    /// <summary>Tính lại theme hiện tại; chỉ phát Changed nếu khác trước (hoặc force).</summary>
    public static void Apply(bool force = true)
    {
        var s = Config.Current.Theme;
        var def = Find(s.ThemeId);
        bool dark = WantDark();
        if (!force && Current != null && Current.Def.Id == def.Id && Current.IsDark == dark) return;

        var p = dark ? def.Dark : def.Light;
        double op = Math.Clamp(s.Opacity ?? 1.0, 0.2, 1.6);

        Color dock1 = Fade(C(p.DockBackground, "#66FFFFFF"), op);
        Color dock2 = Fade(C(p.DockBackground2 ?? p.DockBackground, "#66FFFFFF"), op);
        Brush dockBg;
        if (dock1 != dock2)
        {
            var lg = new LinearGradientBrush(dock1, dock2, 90);
            lg.Freeze(); dockBg = lg;
        }
        else dockBg = B(dock1);

        Color mb1 = Fade(C(p.MenuBarBackground, "#B3FFFFFF"), op);
        Brush mbBg;
        if (!string.IsNullOrEmpty(p.MenuBarBackground2))
        {
            var lg = new LinearGradientBrush(mb1, Fade(C(p.MenuBarBackground2, "#B3FFFFFF"), op), 0);
            lg.Freeze(); mbBg = lg;
        }
        else mbBg = B(mb1);

        Brush plate;
        Color pl1 = C(p.IconPlate, "#FFFFFFFF");
        if (!string.IsNullOrEmpty(p.IconPlate2)) { var lg = new LinearGradientBrush(pl1, C(p.IconPlate2, "#FFEEEEEE"), 90); lg.Freeze(); plate = lg; }
        else plate = B(pl1);

        IconStyle iconStyle = s.IconStyle;
        if (def.IconStyle != null && Enum.TryParse<IconStyle>(def.IconStyle, true, out var tis) && s.IconStyle == IconStyle.Original) iconStyle = tis;

        string fontName = s.FontFamily ?? def.FontFamily;
        var r = new ResolvedTheme
        {
            Def = def,
            IsDark = dark,
            DockBackground = dockBg,
            DockBorder = B(C(p.DockBorder, "#40FFFFFF")),
            DockHighlight = B(C(p.DockHighlight, "#30FFFFFF")),
            Dot = B(C(p.Dot, "#CC000000")),
            LabelBackground = B(C(p.LabelBackground, "#E6FFFFFF")),
            LabelForeground = B(C(p.LabelForeground, "#FF000000")),
            MenuBarBackground = mbBg,
            MenuBarColor = mb1,
            MenuBarForeground = B(C(p.MenuBarForeground, "#FF000000")),
            MenuHighlight = B(C(p.MenuHighlight, "#26000000")),
            PopupBackground = B(C(p.PopupBackground, "#F2FFFFFF")),
            PopupForeground = B(C(p.PopupForeground, "#FF000000")),
            PopupBorder = B(C(p.PopupBorder, "#33000000")),
            Selection = B(C(p.Selection, "#FF0A64D8")),
            SelectionForeground = B(C(p.SelectionForeground, "#FFFFFFFF")),
            Accent = B(C(p.Accent, "#FF0A84FF")),
            AccentColor = C(p.Accent, "#FF0A84FF"),
            IconPlate = plate,
            Separator = B(C(p.Separator, "#33000000")),
            Badge = B(C(p.Badge, "#FFFF3B30")),
            WindowBackground = B(C(p.WindowBackground, dark ? "#FF1E1E20" : "#FFF0F0F3")),
            CardBackground = B(C(p.CardBackground, dark ? "#FF2A2A2D" : "#FFFFFFFF")),
            CornerRadius = s.CornerRadius ?? def.CornerRadius,
            PopupCornerRadius = def.PopupCornerRadius,
            BorderThickness = s.Border == false ? 0 : (s.Border == true && def.BorderThickness <= 0 ? 1 : def.BorderThickness),
            ShadowBlur = def.ShadowBlur,
            ShadowOpacity = def.ShadowOpacity,
            MenuBarMaterial = s.MenuBarMaterial ?? def.MenuBarMaterial,
            Font = new FontFamily(fontName),
            FontSize = def.FontSize,
            IconStyle = iconStyle,
        };
        Current = r;
        PushResources(r);
        Changed?.Invoke();
    }

    static void PushResources(ResolvedTheme r)
    {
        var res = Application.Current?.Resources;
        if (res == null) return;
        res["Md.PopupBg"] = r.PopupBackground;
        res["Md.PopupFg"] = r.PopupForeground;
        res["Md.PopupBorder"] = r.PopupBorder;
        res["Md.Selection"] = r.Selection;
        res["Md.SelectionFg"] = r.SelectionForeground;
        res["Md.Separator"] = r.Separator;
        res["Md.Accent"] = r.Accent;
        res["Md.Font"] = r.Font;
        res["Md.FontSize"] = r.FontSize;
        res["Md.PopupRadius"] = new CornerRadius(r.PopupCornerRadius);
        res["Md.WindowBg"] = r.WindowBackground;
        res["Md.CardBg"] = r.CardBackground;
        var dis = r.PopupForeground.Clone(); dis.Opacity = 0.4; dis.Freeze();
        res["Md.DisabledFg"] = dis;
        var sub = r.PopupForeground.Clone(); sub.Opacity = 0.6; sub.Freeze();
        res["Md.SubtleFg"] = sub;
    }

    // ------------------ Nhập / xuất ------------------

    public static ThemeDefinition EffectiveDefinition()
    {
        var s = Config.Current.Theme;
        var d = Find(s.ThemeId).Clone();
        if (s.CornerRadius != null) d.CornerRadius = s.CornerRadius.Value;
        if (s.Border != null) d.BorderThickness = s.Border.Value ? Math.Max(1, d.BorderThickness) : 0;
        if (s.FontFamily != null) d.FontFamily = s.FontFamily;
        if (s.MenuBarMaterial != null) d.MenuBarMaterial = s.MenuBarMaterial;
        return d;
    }

    public static string SaveAsUserTheme(ThemeDefinition def, string name)
    {
        def = def.Clone();
        def.Name = name;
        def.Id = MakeId(name);
        var dir = Path.Combine(Paths.ThemesDir, def.Id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "theme.json"), JsonSerializer.Serialize(def, Config.JsonOptions));
        return def.Id;
    }

    static string MakeId(string name)
    {
        var chars = name.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => char.IsLetterOrDigit(c) && c < 128 || c == ' ' || c == '-').Select(c => c == ' ' ? '-' : c).ToArray();
        var id = new string(chars).Trim('-');
        if (id.Length == 0) id = "theme";
        string baseId = id; int n = 2;
        while (All().Any(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) id = baseId + "-" + n++;
        return id;
    }

    public static void Export(ThemeDefinition def, string file)
    {
        if (File.Exists(file)) File.Delete(file);
        using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("theme.json");
        using (var w = new StreamWriter(entry.Open())) w.Write(JsonSerializer.Serialize(def, Config.JsonOptions));
        string? iconDir = def.Folder != null ? Path.Combine(def.Folder, "icons") : null;
        if (iconDir != null && Directory.Exists(iconDir))
            foreach (var f in Directory.GetFiles(iconDir))
                zip.CreateEntryFromFile(f, "icons/" + Path.GetFileName(f));
    }

    /// <summary>Nhập file .macdocktheme (zip) hoặc theme.json. Trả về id theme mới.</summary>
    public static string Import(string file)
    {
        ThemeDefinition def;
        string? tempDir = null;
        if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            def = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(file), Config.JsonOptions) ?? throw new InvalidDataException("theme.json rỗng");
        else
        {
            tempDir = Path.Combine(Path.GetTempPath(), "macdock-theme-" + Guid.NewGuid().ToString("N"));
            using (var zip = ZipFile.OpenRead(file))
            {
                foreach (var e in zip.Entries)
                {
                    if (string.IsNullOrEmpty(e.Name)) continue;
                    var rel = e.FullName.Replace('\\', '/');
                    if (rel.Contains("..")) continue; // chống path traversal
                    if (!(rel == "theme.json" || rel.StartsWith("icons/"))) continue;
                    var dest = Path.Combine(tempDir, rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    e.ExtractToFile(dest, true);
                }
            }
            var tj = Path.Combine(tempDir, "theme.json");
            if (!File.Exists(tj)) throw new InvalidDataException("Không có theme.json trong gói theme");
            def = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(tj), Config.JsonOptions) ?? throw new InvalidDataException("theme.json rỗng");
        }
        def.Id = MakeId(string.IsNullOrWhiteSpace(def.Name) ? Path.GetFileNameWithoutExtension(file) : def.Name);
        var dir = Path.Combine(Paths.ThemesDir, def.Id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "theme.json"), JsonSerializer.Serialize(def, Config.JsonOptions));
        if (tempDir != null)
        {
            var icons = Path.Combine(tempDir, "icons");
            if (Directory.Exists(icons))
            {
                Directory.CreateDirectory(Path.Combine(dir, "icons"));
                foreach (var f in Directory.GetFiles(icons)) File.Copy(f, Path.Combine(dir, "icons", Path.GetFileName(f)), true);
            }
            try { Directory.Delete(tempDir, true); } catch { }
        }
        return def.Id;
    }

    public static void DeleteUserTheme(string id)
    {
        var t = All().FirstOrDefault(x => x.Id == id);
        if (t?.Folder != null && !t.BuiltIn) Directory.Delete(t.Folder, true);
    }

    // ------------------ Icon pack ------------------

    public static IEnumerable<(string name, string folder)> IconPacks()
    {
        foreach (var t in LoadUserThemes())
        {
            var d = Path.Combine(t.Folder!, "icons");
            if (Directory.Exists(d)) yield return (t.Name, d);
        }
        foreach (var d in Directory.GetDirectories(Paths.IconsDir))
            yield return (Path.GetFileName(d), d);
    }

    /// <summary>Tìm icon thay thế cho app: icon riêng của người dùng, rồi icon pack.</summary>
    public static string? FindIconOverride(string key, string? exePath, string? aumid)
    {
        var s = Config.Current.Theme;
        if (s.IconOverrides.TryGetValue(key, out var custom) && File.Exists(custom)) return custom;
        string? pack = s.IconPack;
        if (string.IsNullOrEmpty(pack) || !Directory.Exists(pack)) return null;
        var names = new List<string>();
        if (exePath != null) names.Add(Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant());
        if (aumid != null)
        {
            names.Add(aumid.ToLowerInvariant().Replace('!', '_'));
            var fam = aumid.Split('!')[0];
            names.Add(fam.ToLowerInvariant());
            names.Add(fam.Split('_')[0].ToLowerInvariant());
        }
        if (key.StartsWith("special:")) names.Add(key.Substring(8));
        foreach (var n in names)
            foreach (var ext in new[] { ".png", ".ico" })
            {
                var f = Path.Combine(pack, n + ext);
                if (File.Exists(f)) return f;
            }
        return null;
    }

    // ------------------ Theme dựng sẵn ------------------

    static List<ThemeDefinition> CreateBuiltIns()
    {
        var list = new List<ThemeDefinition>();

        list.Add(new ThemeDefinition
        {
            Id = "sonoma", Name = "Sonoma", Author = "MacDock", BuiltIn = true,
            Light = new ThemePalette(),
            Dark = new ThemePalette
            {
                DockBackground = "#73323236", DockBackground2 = "#80282830", DockBorder = "#33FFFFFF", DockHighlight = "#26FFFFFF", Dot = "#CCF5F5F7",
                LabelBackground = "#E62C2C30", LabelForeground = "#FFF5F5F7", MenuBarBackground = "#A61E1E22", MenuBarForeground = "#FFF5F5F7",
                MenuHighlight = "#33FFFFFF", PopupBackground = "#F02A2A2E", PopupForeground = "#FFF2F2F4", PopupBorder = "#40FFFFFF",
                Selection = "#FF0A64D8", IconPlate = "#FF3A3A3F", IconPlate2 = "#FF2A2A2E", Separator = "#33FFFFFF",
                WindowBackground = "#FF1E1E20", CardBackground = "#FF2B2B2E"
            }
        });

        list.Add(new ThemeDefinition
        {
            Id = "midnight", Name = "Nửa đêm", Author = "MacDock", BuiltIn = true, CornerRadius = 20,
            Light = new ThemePalette
            {
                DockBackground = "#8CDDE3F5", DockBackground2 = "#99C9D2EE", DockBorder = "#80FFFFFF", Dot = "#CC1B2A55",
                MenuBarBackground = "#B8E4E8F6", MenuBarForeground = "#FF101A3A", Selection = "#FF3557D8", Accent = "#FF3557D8",
                PopupBackground = "#F5EEF1FA", PopupForeground = "#FF101A3A", IconPlate = "#FFFFFFFF", IconPlate2 = "#FFDCE3F7"
            },
            Dark = new ThemePalette
            {
                DockBackground = "#8C141A33", DockBackground2 = "#99101429", DockBorder = "#3380A0FF", DockHighlight = "#2080A0FF", Dot = "#FF9DB4FF",
                LabelBackground = "#EB18203D", LabelForeground = "#FFE6ECFF", MenuBarBackground = "#B30E1328", MenuBarForeground = "#FFE6ECFF",
                MenuHighlight = "#3380A0FF", PopupBackground = "#F2151B33", PopupForeground = "#FFE6ECFF", PopupBorder = "#4080A0FF",
                Selection = "#FF4A6CF0", Accent = "#FF6E8BFF", IconPlate = "#FF27305A", IconPlate2 = "#FF161C38", Separator = "#3380A0FF",
                WindowBackground = "#FF10142A", CardBackground = "#FF1A2142"
            }
        });

        list.Add(new ThemeDefinition
        {
            Id = "glass", Name = "Kính trong", Author = "MacDock", BuiltIn = true, CornerRadius = 22, ShadowOpacity = 0.18, MenuBarMaterial = "acrylic",
            Light = new ThemePalette
            {
                DockBackground = "#33FFFFFF", DockBackground2 = "#26FFFFFF", DockBorder = "#99FFFFFF", DockHighlight = "#66FFFFFF",
                MenuBarBackground = "#59FFFFFF", PopupBackground = "#E6FAFAFC"
            },
            Dark = new ThemePalette
            {
                DockBackground = "#33000000", DockBackground2 = "#40000000", DockBorder = "#40FFFFFF", DockHighlight = "#33FFFFFF", Dot = "#E6FFFFFF",
                LabelBackground = "#D9202024", LabelForeground = "#FFFFFFFF", MenuBarBackground = "#59000000", MenuBarForeground = "#FFFFFFFF",
                MenuHighlight = "#33FFFFFF", PopupBackground = "#E6242428", PopupForeground = "#FFF5F5F7", PopupBorder = "#40FFFFFF",
                IconPlate = "#FF3A3A3F", IconPlate2 = "#FF2A2A2E", Separator = "#40FFFFFF", WindowBackground = "#FF1E1E20", CardBackground = "#FF2B2B2E"
            }
        });

        list.Add(new ThemeDefinition
        {
            Id = "sunset", Name = "Hoàng hôn", Author = "MacDock", BuiltIn = true, CornerRadius = 20,
            Light = new ThemePalette
            {
                DockBackground = "#99FFE3D1", DockBackground2 = "#A6FFC9C9", DockBorder = "#99FFFFFF", Dot = "#CC7A2E1F",
                MenuBarBackground = "#BFFFE0CC", MenuBarBackground2 = "#BFFFD0DC", MenuBarForeground = "#FF3B1A12",
                Selection = "#FFE5533D", Accent = "#FFFF6B4A", PopupBackground = "#F5FFF4EE", PopupForeground = "#FF3B1A12",
                IconPlate = "#FFFFFFFF", IconPlate2 = "#FFFFE6DA"
            },
            Dark = new ThemePalette
            {
                DockBackground = "#99401C24", DockBackground2 = "#A62B1424", DockBorder = "#40FFB199", DockHighlight = "#26FFC4A8", Dot = "#FFFFB199",
                LabelBackground = "#EB3A1C22", LabelForeground = "#FFFFECE4", MenuBarBackground = "#BF2E1418", MenuBarBackground2 = "#BF2A1226", MenuBarForeground = "#FFFFECE4",
                MenuHighlight = "#33FFB199", PopupBackground = "#F2321A20", PopupForeground = "#FFFFECE4", PopupBorder = "#40FFB199",
                Selection = "#FFE5533D", Accent = "#FFFF8A6B", IconPlate = "#FF55303A", IconPlate2 = "#FF3A1E26", Separator = "#40FFB199",
                WindowBackground = "#FF22121A", CardBackground = "#FF32202A"
            }
        });

        list.Add(new ThemeDefinition
        {
            Id = "forest", Name = "Rừng thông", Author = "MacDock", BuiltIn = true, CornerRadius = 16,
            Light = new ThemePalette
            {
                DockBackground = "#8CE2F0E4", DockBackground2 = "#99CFE6D3", DockBorder = "#99FFFFFF", Dot = "#CC1E4D2B",
                MenuBarBackground = "#BFE3F1E6", MenuBarForeground = "#FF12301B", Selection = "#FF2F8F4E", Accent = "#FF34C759",
                PopupBackground = "#F5F1F8F2", PopupForeground = "#FF12301B", IconPlate = "#FFFFFFFF", IconPlate2 = "#FFE0EFE3"
            },
            Dark = new ThemePalette
            {
                DockBackground = "#99142A1C", DockBackground2 = "#A60E2016", DockBorder = "#4080E0A0", DockHighlight = "#2080E0A0", Dot = "#FF8FE3A8",
                LabelBackground = "#EB15291C", LabelForeground = "#FFE4F5E8", MenuBarBackground = "#BF0F2016", MenuBarForeground = "#FFE4F5E8",
                MenuHighlight = "#3380E0A0", PopupBackground = "#F2142419", PopupForeground = "#FFE4F5E8", PopupBorder = "#4080E0A0",
                Selection = "#FF2F8F4E", Accent = "#FF4CD97B", IconPlate = "#FF24402D", IconPlate2 = "#FF15291C", Separator = "#4080E0A0",
                WindowBackground = "#FF0F1C14", CardBackground = "#FF18291E"
            }
        });

        list.Add(new ThemeDefinition
        {
            Id = "minimal", Name = "Tối giản", Author = "MacDock", BuiltIn = true, CornerRadius = 10, PopupCornerRadius = 6, BorderThickness = 0,
            ShadowBlur = 14, ShadowOpacity = 0.22, MenuBarMaterial = "solid", DockMaterial = "solid", FontFamily = "Segoe UI",
            Light = new ThemePalette
            {
                DockBackground = "#F2FFFFFF", DockBorder = "#00FFFFFF", DockHighlight = "#00FFFFFF", MenuBarBackground = "#FFFFFFFF",
                PopupBackground = "#FFFFFFFF", IconPlate = "#FFF4F4F6", IconPlate2 = null
            },
            Dark = new ThemePalette
            {
                DockBackground = "#F21C1C1E", DockBorder = "#00FFFFFF", DockHighlight = "#00FFFFFF", Dot = "#FFE5E5EA",
                LabelBackground = "#FF2C2C2E", LabelForeground = "#FFFFFFFF", MenuBarBackground = "#FF1C1C1E", MenuBarForeground = "#FFF2F2F7",
                MenuHighlight = "#33FFFFFF", PopupBackground = "#FF2C2C2E", PopupForeground = "#FFF2F2F7", PopupBorder = "#33FFFFFF",
                IconPlate = "#FF3A3A3C", IconPlate2 = null, Separator = "#33FFFFFF", WindowBackground = "#FF1C1C1E", CardBackground = "#FF2C2C2E"
            }
        });

        return list;
    }

    /// <summary>Ghi các theme dựng sẵn ra thư mục để người dùng tham khảo/sửa.</summary>
    public static void WriteSamples()
    {
        try
        {
            var dir = Path.Combine(Paths.Root, "theme-mau");
            Directory.CreateDirectory(dir);
            foreach (var t in BuiltIns)
            {
                var f = Path.Combine(dir, t.Id + ".json");
                if (!File.Exists(f)) File.WriteAllText(f, JsonSerializer.Serialize(t, Config.JsonOptions));
            }
        }
        catch { }
    }
}
