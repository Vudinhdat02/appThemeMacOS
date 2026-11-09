using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MacDock;

[StructLayout(LayoutKind.Sequential)]
public struct SIZE { public int cx, cy; public SIZE(int x, int y) { cx = x; cy = y; } }

[ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItemImageFactory
{
    [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
}

[ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItem
{
    [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int GetParent(out IShellItem ppsi);
    [PreserveSig] int GetDisplayName(uint sigdn, out IntPtr ppszName);
    [PreserveSig] int GetAttributes(uint mask, out uint attribs);
    [PreserveSig] int Compare(IShellItem psi, uint hint, out int order);
}

public static class ShellIcons
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

    static readonly Guid IID_IShellItemImageFactory = new("BCC18B79-BA16-442F-80C4-8A59C30C463B");
    static readonly Guid IID_IShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    const int SIIGBF_BIGGERSIZEOK = 0x1, SIIGBF_ICONONLY = 0x4;
    public const uint SIGDN_NORMALDISPLAY = 0;

    /// <summary>Lấy icon của một đường dẫn shell (file, thư mục, shell:AppsFolder\AUMID ...). Phải gọi trên luồng STA.</summary>
    public static BitmapSource? GetIcon(string parsingName, int size)
    {
        object? obj = null;
        try
        {
            var iid = IID_IShellItemImageFactory;
            if (SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out obj) != 0 || obj is not IShellItemImageFactory f)
                return null;
            if (f.GetImage(new SIZE(size, size), SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out IntPtr hbm) != 0 || hbm == IntPtr.Zero)
                return null;
            try { return FromHBitmap(hbm); }
            finally { Native.DeleteObject(hbm); }
        }
        catch (Exception ex)
        {
            Log.Debug($"GetIcon({parsingName}) lỗi: {ex.Message}");
            return null;
        }
        finally
        {
            if (obj != null && Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj);
        }
    }

    public static string? GetDisplayName(string parsingName)
    {
        object? obj = null;
        try
        {
            var iid = IID_IShellItem;
            if (SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out obj) != 0 || obj is not IShellItem item) return null;
            if (item.GetDisplayName(SIGDN_NORMALDISPLAY, out IntPtr p) != 0 || p == IntPtr.Zero) return null;
            string? s = Marshal.PtrToStringUni(p);
            Marshal.FreeCoTaskMem(p);
            return s;
        }
        catch { return null; }
        finally
        {
            if (obj != null && Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj);
        }
    }

    public static BitmapSource? FromHBitmap(IntPtr hbm)
    {
        var bmp = new BITMAP();
        if (Native.GetObject(hbm, Marshal.SizeOf<BITMAP>(), ref bmp) == 0) return null;
        int w = bmp.bmWidth, h = Math.Abs(bmp.bmHeight);
        if (w <= 0 || h <= 0) return null;
        var bi = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = w,
            biHeight = -h, // top-down
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0
        };
        var bits = new byte[w * h * 4];
        IntPtr hdc = Native.GetDC(IntPtr.Zero);
        try
        {
            if (Native.GetDIBits(hdc, hbm, 0, (uint)h, bits, ref bi, 0) == 0) return null;
        }
        finally { Native.ReleaseDC(IntPtr.Zero, hdc); }

        bool anyAlpha = false;
        for (int i = 3; i < bits.Length; i += 4) if (bits[i] != 0) { anyAlpha = true; break; }
        if (!anyAlpha) for (int i = 3; i < bits.Length; i += 4) bits[i] = 255;

        var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, bits, w * 4);
        src.Freeze();
        return src;
    }

    public static BitmapSource? FromHIcon(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero) return null;
        try
        {
            var src = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var copy = new WriteableBitmap(src);
            copy.Freeze();
            return copy;
        }
        catch { return null; }
    }

    public static BitmapSource? LoadImageFile(string path, int size)
    {
        try
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".exe" or ".dll" or ".lnk") return GetIcon(path, size);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(path);
            if (ext != ".ico") bi.DecodePixelWidth = size;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch (Exception ex)
        {
            Log.Warn($"Không đọc được ảnh {path}: {ex.Message}");
            return null;
        }
    }
}

/// <summary>Bộ đệm icon nạp trên một luồng STA riêng để không chặn UI.</summary>
public static class IconCache
{
    static readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);
    static readonly BlockingCollection<(string key, string parsing, int size, Action<ImageSource?> done)> _queue = new();
    static Thread? _thread;
    static System.Windows.Threading.Dispatcher? _ui;

    public static int IconPixelSize { get; set; } = 128;

    public static void Init(System.Windows.Threading.Dispatcher ui)
    {
        _ui = ui;
        _thread = new Thread(Worker) { IsBackground = true, Name = "IconLoader" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    static void Worker()
    {
        foreach (var job in _queue.GetConsumingEnumerable())
        {
            ImageSource? img = null;
            try
            {
                if (!_cache.TryGetValue(job.key, out img))
                {
                    img = ShellIcons.GetIcon(job.parsing, job.size);
                    _cache[job.key] = img;
                    if (img is BitmapSource bs) SaveDisk(job.key, bs);
                }
            }
            catch (Exception ex) { Log.Debug("Icon worker: " + ex.Message); }
            var result = img;
            _ui?.BeginInvoke(() => job.done(result));
        }
    }

    /// <summary>Lấy icon (đồng bộ nếu đã có trong cache, ngược lại nạp nền và gọi callback).</summary>
    static string DiskDir
    {
        get
        {
            var d = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacDock", "iconcache");
            System.IO.Directory.CreateDirectory(d);
            return d;
        }
    }

    static string DiskFile(string key)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(key.ToLowerInvariant())));
        return System.IO.Path.Combine(DiskDir, hash + ".png");
    }

    static void SaveDisk(string key, BitmapSource bs)
    {
        try
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bs));
            using var fs = System.IO.File.Create(DiskFile(key));
            enc.Save(fs);
        }
        catch { }
    }

    static BitmapSource? LoadDisk(string key)
    {
        try
        {
            var f = DiskFile(key);
            var fi = new System.IO.FileInfo(f);
            if (!fi.Exists || (DateTime.Now - fi.LastWriteTime).TotalDays > 7) return null;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(f);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch { return null; }
    }

    public static ImageSource? Get(string parsingName, Action<ImageSource?> onLoaded)
    {
        string key = parsingName + "|" + IconPixelSize;
        if (_cache.TryGetValue(key, out var img)) return img;
        // cache trên đĩa: có icon ngay khi vừa đăng nhập, không phải chờ shell
        var disk = LoadDisk(key);
        if (disk != null) { _cache[key] = disk; return disk; }
        _queue.Add((key, parsingName, IconPixelSize, onLoaded));
        return null;
    }

    public static void Clear() => _cache.Clear();
}
