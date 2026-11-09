using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MacDock;

/// <summary>Đọc độ sáng phần trên của hình nền để chọn màu chữ cho Thanh Menu trong suốt.</summary>
public static class Wallpaper
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool SystemParametersInfo(uint action, uint param, StringBuilder sb, uint winIni);

    static double? _lum;
    static readonly Dictionary<string, BitmapSource?> _strips = new();

    public static void Invalidate() { _lum = null; _strips.Clear(); }

    public static string? Path()
    {
        var sb = new StringBuilder(520);
        SystemParametersInfo(0x73, (uint)sb.Capacity, sb, 0);
        var p = sb.ToString();
        return !string.IsNullOrWhiteSpace(p) && File.Exists(p) ? p : null;
    }

    /// <summary>
    /// Dải trên cùng của hình nền ở màn hình <paramref name="mon"/> (pixel vật lý), đã làm mờ.
    /// Thanh Menu là AppBar nên phía sau nó luôn là hình nền: làm mờ trước một lần là đủ, không tốn CPU khi chạy.
    /// </summary>
    public static BitmapSource? BlurredStrip(RECT mon, int heightPx, double blur)
    {
        string key = $"{mon}|{heightPx}|{blur:0}";
        if (_strips.TryGetValue(key, out var cached)) return cached;
        BitmapSource? result = null;
        try
        {
            var path = Path();
            if (path != null)
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bi.UriSource = new Uri(path);
                bi.DecodePixelWidth = Math.Min(3840, mon.Width + 200);
                bi.EndInit();
                double iw = bi.PixelWidth, ih = bi.PixelHeight;
                // kiểu "Fill": phóng cho phủ kín màn hình, căn giữa; phóng thêm chút để có viền khi làm mờ
                double s = Math.Max(mon.Width / iw, mon.Height / ih) * 1.06;
                double ox = (iw * s - mon.Width) / 2, oy = (ih * s - mon.Height) / 2;
                int pad = (int)Math.Ceiling(blur * 1.5);
                int w = mon.Width, h = heightPx + pad;
                var img = new System.Windows.Controls.Image { Source = bi, Width = iw * s, Height = ih * s, Stretch = Stretch.Fill };
                System.Windows.Controls.Canvas.SetLeft(img, -ox);
                System.Windows.Controls.Canvas.SetTop(img, -oy);
                var canvas = new System.Windows.Controls.Canvas { Width = w, Height = h, ClipToBounds = true, Children = { img } };
                canvas.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = blur, KernelType = System.Windows.Media.Effects.KernelType.Gaussian, RenderingBias = System.Windows.Media.Effects.RenderingBias.Quality };
                canvas.Measure(new System.Windows.Size(w, h));
                canvas.Arrange(new System.Windows.Rect(0, 0, w, h));
                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(canvas);
                result = new CroppedBitmap(rtb, new System.Windows.Int32Rect(0, 0, w, heightPx));
                result.Freeze();
            }
        }
        catch (Exception ex) { Log.Debug("Wallpaper strip: " + ex.Message); }
        _strips[key] = result;
        return result;
    }

    /// <summary>Độ sáng 0..1 của dải trên cùng hình nền.</summary>
    public static double TopLuminance()
    {
        if (_lum != null) return _lum.Value;
        double lum = 0.5;
        try
        {
            var sb = new StringBuilder(520);
            SystemParametersInfo(0x73, (uint)sb.Capacity, sb, 0);
            string path = sb.ToString();
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bi.UriSource = new Uri(path);
                bi.DecodePixelWidth = 160;
                bi.EndInit();
                var src = new FormatConvertedBitmap(bi, PixelFormats.Bgra32, null, 0);
                int w = src.PixelWidth, h = Math.Max(1, src.PixelHeight / 14);
                var px = new byte[w * h * 4];
                src.CopyPixels(new System.Windows.Int32Rect(0, 0, w, h), px, w * 4, 0);
                double sum = 0;
                for (int i = 0; i < px.Length; i += 4) sum += (0.0722 * px[i] + 0.7152 * px[i + 1] + 0.2126 * px[i + 2]) / 255.0;
                lum = sum / (w * h);
            }
            else
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors");
                if (k?.GetValue("Background") is string s)
                {
                    var p = s.Split(' ').Select(x => double.TryParse(x, out var d) ? d : 0).ToArray();
                    if (p.Length == 3) lum = (0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2]) / 255.0;
                }
            }
        }
        catch (Exception ex) { Log.Debug("Wallpaper: " + ex.Message); }
        _lum = lum;
        return lum;
    }
}
