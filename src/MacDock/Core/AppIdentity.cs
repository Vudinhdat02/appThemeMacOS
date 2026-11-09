using System.Collections.Concurrent;
using System.Diagnostics;

namespace MacDock;

public sealed record AppId(string Key, string? ExePath, string? Aumid, uint Pid);

/// <summary>Xác định "ứng dụng" của một cửa sổ: AUMID cho app đóng gói, đường dẫn exe cho app thường.</summary>
public static class AppIdentity
{
    static readonly ConcurrentDictionary<IntPtr, AppId> _cache = new();
    static readonly ConcurrentDictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    static readonly ConcurrentDictionary<uint, string?> _procPath = new();

    public static readonly string ExplorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
    public static string OwnKey => Paths.ExePath.ToLowerInvariant();

    public static string? ProcessPath(uint pid)
    {
        if (_procPath.TryGetValue(pid, out var p)) return p;
        p = Native.GetProcessPath(pid);
        if (_procPath.Count > 500) _procPath.Clear();
        _procPath[pid] = p;
        return p;
    }

    public static bool IsPackagedPath(string? path) =>
        path != null && (path.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) ||
                         path.Contains(@"\SystemApps\", StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith(@"\ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase));

    public static AppId? Resolve(IntPtr hwnd)
    {
        if (_cache.TryGetValue(hwnd, out var id))
        {
            Native.GetWindowThreadProcessId(hwnd, out uint cpid);
            if (cpid == id.Pid) return id;
        }
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;
        string? path = ProcessPath(pid);
        string? aumid = null;
        bool afh = path != null && path.EndsWith(@"\ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);

        if (IsPackagedPath(path))
        {
            aumid = Native.GetWindowAumid(hwnd);
            if (aumid == null)
            {
                uint realPid = pid;
                if (afh)
                {
                    // Tìm CoreWindow con thuộc tiến trình app thật
                    Native.EnumChildWindows(hwnd, (c, _) =>
                    {
                        if (Native.GetClass(c) == "Windows.UI.Core.CoreWindow")
                        {
                            Native.GetWindowThreadProcessId(c, out uint cp);
                            if (cp != pid) { realPid = cp; return false; }
                        }
                        return true;
                    }, IntPtr.Zero);
                }
                aumid = Native.GetProcessAumid(realPid);
            }
            if (aumid == null && afh) return null; // frame chưa sẵn sàng, thử lại sau
        }

        AppId result = aumid != null
            ? new AppId("aumid:" + aumid.ToLowerInvariant(), path, aumid, pid)
            : new AppId((path ?? ("pid:" + pid)).ToLowerInvariant(), path, null, pid);
        _cache[hwnd] = result;
        return result;
    }

    public static void Forget(IntPtr hwnd) => _cache.TryRemove(hwnd, out _);

    public static string DisplayName(string key, string? exePath, string? aumid, string? fallback = null)
    {
        if (_names.TryGetValue(key, out var n)) return n;
        string? name = null;
        try
        {
            if (aumid != null)
                name = ShellIcons.GetDisplayName(@"shell:AppsFolder\" + aumid);
            else if (key.StartsWith("folder:"))
                name = Path.GetFileName(key.Substring(7).TrimEnd('\\')) is { Length: > 0 } f ? f : key.Substring(7);
            else if (exePath != null)
            {
                if (exePath.Equals(ExplorerPath, StringComparison.OrdinalIgnoreCase)) name = "File Explorer";
                else if (exePath.Equals(Paths.ExePath, StringComparison.OrdinalIgnoreCase)) name = "MacDock";
                else if (File.Exists(exePath))
                {
                    var vi = FileVersionInfo.GetVersionInfo(exePath);
                    name = !string.IsNullOrWhiteSpace(vi.FileDescription) ? vi.FileDescription : vi.ProductName;
                    if (name != null && (name.Length > 40 || name.Contains("Host", StringComparison.Ordinal))) name = vi.ProductName ?? name;
                }
            }
        }
        catch { }
        if (string.IsNullOrWhiteSpace(name)) name = fallback;
        if (string.IsNullOrWhiteSpace(name)) name = exePath != null ? Path.GetFileNameWithoutExtension(exePath) : key;
        name = name!.Trim();
        _names[key] = name;
        return name;
    }

    public static string IconParsingName(string key, string? exePath, string? aumid)
    {
        if (aumid != null) return @"shell:AppsFolder\" + aumid;
        if (key.StartsWith("folder:")) return key.Substring(7);
        return exePath ?? key;
    }

    public static bool AumidExists(string aumid) => ShellIcons.GetDisplayName(@"shell:AppsFolder\" + aumid) != null;

    public static string? ResolveShortcut(string lnk)
    {
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return null;
            dynamic sh = Activator.CreateInstance(t)!;
            dynamic sc = sh.CreateShortcut(lnk);
            string target = sc.TargetPath;
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sc);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sh);
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch { return null; }
    }
}
