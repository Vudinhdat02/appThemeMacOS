using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MacDock;

/// <summary>Bắt phím âm lượng (hook bàn phím cấp thấp trên luồng riêng) và theo dõi độ sáng để hiện OSD kiểu macOS.</summary>
public static class MediaKeys
{
    delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, LowLevelProc proc, IntPtr hMod, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint tid, uint msg, IntPtr w, IntPtr l);

    const int VK_VOLUME_MUTE = 0xAD, VK_VOLUME_DOWN = 0xAE, VK_VOLUME_UP = 0xAF;
    static Thread? _thread;
    static uint _tid;
    static LowLevelProc? _proc;
    static Dispatcher? _ui;
    static System.Management.ManagementEventWatcher? _bright;

    public static void Apply(Dispatcher ui)
    {
        _ui = ui;
        bool want = Config.Current.General.OsdStyle == "macdock";
        if (want) Start(); else Stop();
    }

    static void Start()
    {
        if (_thread != null) return;
        _thread = new Thread(() =>
        {
            _tid = Native.GetCurrentThreadId();
            _proc = Hook;
            IntPtr h = SetWindowsHookEx(13, _proc, Native.GetModuleHandle(null), 0);
            if (h == IntPtr.Zero) { Log.Warn("Không cài được hook phím âm lượng"); return; }
            while (Native.GetMessage(out var m, IntPtr.Zero, 0, 0) > 0) { Native.TranslateMessage(ref m); Native.DispatchMessage(ref m); }
            UnhookWindowsHookEx(h);
        }) { IsBackground = true, Name = "MediaKeys" };
        _thread.Start();
        try
        {
            _bright = new System.Management.ManagementEventWatcher(new System.Management.ManagementScope(@"root\WMI"),
                new System.Management.EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
            _bright.EventArrived += (_, e) =>
            {
                try
                {
                    int v = Convert.ToInt32(e.NewEvent["Brightness"]);
                    _ui?.BeginInvoke(() => OsdWindow.ShowLevel(OsdKind.Brightness, v / 100.0, false));
                }
                catch { }
            };
            _bright.Start();
        }
        catch (Exception ex) { Log.Debug("Brightness watcher: " + ex.Message); _bright = null; }
    }

    public static void Stop()
    {
        if (_thread != null && _tid != 0) PostThreadMessage(_tid, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread = null; _tid = 0;
        try { _bright?.Stop(); _bright?.Dispose(); } catch { }
        _bright = null;
    }

    static IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vk = Marshal.ReadInt32(lParam);
            if (vk is VK_VOLUME_MUTE or VK_VOLUME_DOWN or VK_VOLUME_UP)
            {
                int msg = wParam.ToInt32();
                if (msg is 0x100 or 0x104) _ui?.BeginInvoke(() => OnVolumeKey(vk));
                return new IntPtr(1); // nuốt phím để Windows không hiện OSD của nó
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    static void OnVolumeKey(int vk)
    {
        var a = AudioVolume.Instance;
        if (!a.Available) return;
        if (vk == VK_VOLUME_MUTE) a.Muted = !a.Muted;
        else
        {
            float lv = (float)(Math.Round(a.Level * 50) / 50.0); // bước 2% như Windows
            lv = Math.Clamp(lv + (vk == VK_VOLUME_UP ? 0.02f : -0.02f), 0f, 1f);
            a.Level = lv;
            if (a.Muted && vk == VK_VOLUME_UP) a.Muted = false;
        }
        OsdWindow.ShowLevel(OsdKind.Volume, a.Level, a.Muted);
    }
}

public enum OsdKind { Volume, Brightness }

/// <summary>Bảng âm lượng/độ sáng dạng kính kiểu macOS 26 (không lấy focus).</summary>
public sealed class OsdWindow : Window
{
    static OsdWindow? _inst;
    readonly Border _card;
    readonly Border _fill;
    readonly ContentControl _icon = new();
    readonly TextBlock _label = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    readonly DispatcherTimer _hide;
    const double TrackW = 196;

    OsdWindow()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight; Focusable = false; IsHitTestVisible = false;
        var t = ThemeManager.Current;
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        _icon.Margin = new Thickness(0, 0, 8, 0);
        top.Children.Add(_icon); top.Children.Add(_label);
        var track = new Border { Width = TrackW, Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x88)), HorizontalAlignment = HorizontalAlignment.Left };
        _fill = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = t.PopupForeground, HorizontalAlignment = HorizontalAlignment.Left };
        track.Child = _fill;
        var sp = new StackPanel(); sp.Children.Add(top); sp.Children.Add(track);
        _card = new Border
        {
            Child = sp, Padding = new Thickness(16, 13, 16, 15), CornerRadius = new CornerRadius(18), Margin = new Thickness(16),
            Background = t.PopupBackground, BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)), BorderThickness = new Thickness(0.8),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.25, Direction = 270 }
        };
        TextElement.SetForeground(_card, t.PopupForeground);
        TextElement.SetFontFamily(_card, t.Font);
        Content = _card;
        _hide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _hide.Tick += (_, _) =>
        {
            _hide.Stop();
            var an = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220));
            an.Completed += (_, _) => { if (Opacity < 0.05) Hide(); };
            BeginAnimation(OpacityProperty, an);
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var h = new WindowInteropHelper(this).Handle;
        Native.AddExStyle(h, Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT);
    }

    public static void ShowLevel(OsdKind kind, double level, bool muted)
    {
        if (Config.Current.General.OsdStyle != "macdock") return;
        if (_inst == null || ThemeManager.Current.Def.Id + ThemeManager.Current.IsDark != _themeKey) { _inst?.Close(); _inst = new OsdWindow(); _themeKey = ThemeManager.Current.Def.Id + ThemeManager.Current.IsDark; }
        _inst.Update(kind, level, muted);
    }
    static string _themeKey = "";

    void Update(OsdKind kind, double level, bool muted)
    {
        var fg = ThemeManager.Current.PopupForeground;
        _icon.Content = kind == OsdKind.Volume ? Glyphs.VolumeIcon(fg, level, muted, 18) : Glyphs.Icon(Glyphs.Sun, fg, 18);
        _label.Text = kind == OsdKind.Volume ? (muted ? L.T("Âm thanh — tắt tiếng") : L.F("Âm thanh  {0}%", Math.Round(level * 100))) : L.F("Độ sáng  {0}%", Math.Round(level * 100));
        double target = TrackW * Math.Clamp(muted ? 0 : level, 0, 1);
        if (App.EffectsOff || !IsVisible) _fill.Width = target;
        else _fill.BeginAnimation(WidthProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(120)));
        if (!IsVisible) { Opacity = 0; Show(); }
        Place();
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(App.EffectsOff ? 0 : 120)));
        _hide.Stop(); _hide.Start();
    }

    void Place()
    {
        Native.GetCursorPos(out var pt);
        var hmon = Native.MonitorFromPoint(pt, Native.MONITOR_DEFAULTTONEAREST);
        var m = Monitors.FromHandle(hmon) ?? Monitors.GetAll().First();
        double s = m.Scale;
        UpdateLayout();
        double w = ActualWidth, h = ActualHeight;
        double x, y;
        if (Config.Current.General.OsdPosition == "bottom")
        {
            x = (m.Bounds.Left + m.Bounds.Width / 2.0) / s - w / 2;
            y = m.Bounds.Bottom / s - h - 150;
        }
        else
        {
            x = m.Bounds.Right / s - w - 2;
            y = m.Bounds.Top / s + Config.Current.MenuBar.Height - 6;
        }
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, (int)(x * s), (int)(y * s), 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }
}
