using System.Diagnostics;

namespace MacDock;

/// <summary>Đo nhịp khung hình của Dock khi đang hiệu ứng (chỉ dùng để chẩn đoán, --test).</summary>
public static class FrameStats
{
    static readonly Stopwatch _sw = Stopwatch.StartNew();
    static readonly List<double> _gaps = new(), _work = new(), _render = new();
    static double _last = -1;
    public static bool On;

    public static void Reset() { lock (_gaps) { _gaps.Clear(); _work.Clear(); _render.Clear(); _last = -1; On = true; } }
    public static double Ms => _sw.Elapsed.TotalMilliseconds;

    /// <summary>Gọi mỗi khi Dock thực sự vẽ một khung (đang hiệu ứng).</summary>
    public static void Frame(double workMs)
    {
        if (!On) return;
        double now = Ms;
        lock (_gaps) { if (_last >= 0 && now - _last < 200) _gaps.Add(now - _last); _work.Add(workMs); _last = now; }
    }
    public static void Render(double ms) { if (On) lock (_gaps) _render.Add(ms); }

    static string P(List<double> l) => l.Count == 0 ? "-" : $"n={l.Count} avg={l.Average():F1} p50={Q(l, .5):F1} p95={Q(l, .95):F1} max={l.Max():F1}";
    static double Q(List<double> l, double q) { var s = l.OrderBy(x => x).ToList(); return s[Math.Min(s.Count - 1, (int)(q * s.Count))]; }

    public static string Report()
    {
        lock (_gaps)
        {
            double fps = _gaps.Count > 0 ? 1000 / _gaps.Average() : 0;
            int janks = _gaps.Count(g => g > 25);
            return $"FrameStats tier={System.Windows.Media.RenderCapability.Tier >> 16} fps≈{fps:F0} janks(>25ms)={janks} | gap {P(_gaps)} | tick {P(_work)} | render {P(_render)}";
        }
    }
}
