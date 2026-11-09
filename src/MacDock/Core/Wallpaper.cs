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

    public static void Invalidate() => _lum = null;

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
