using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace MacDock;

/// <summary>Trạng thái hệ thống cho Menu Bar: pin, mạng, âm lượng, bộ gõ, đồng hồ.</summary>
public sealed class StatusService
{
    public static StatusService Instance { get; } = new();

    public int BatteryPercent { get; private set; } = -1; // -1 = không có pin
    public bool Charging { get; private set; }
    public bool OnBattery { get; private set; }
    public NetStatus Net { get; private set; } = new(NetKind.None, "", 0);
    public float Volume { get; private set; }
    public bool Muted { get; private set; }
    public bool AudioAvailable { get; private set; }
    public string InputLang { get; private set; } = "";
    public IntPtr InputHkl { get; private set; }
    public string ClockText { get; private set; } = "";

    public event Action? Changed;
    public event Action? ClockChanged;

    DispatcherTimer? _slow, _lang, _clock;


    public void Start(Dispatcher d)
    {
        _slow = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _slow.Tick += (_, _) => { RefreshPower(); RefreshNet(); Changed?.Invoke(); };
        _slow.Start();
        _lang = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _lang.Tick += (_, _) => { if (RefreshLang()) Changed?.Invoke(); };
        _lang.Start();
        _clock = new DispatcherTimer();
        _clock.Tick += (_, _) => UpdateClock();
        NetworkChange.NetworkAddressChanged += (_, _) => d.BeginInvoke(() => { RefreshNet(); Changed?.Invoke(); });
        NetworkChange.NetworkAvailabilityChanged += (_, _) => d.BeginInvoke(() => { RefreshNet(); Changed?.Invoke(); });
        AudioVolume.Instance.Changed += () => d.BeginInvoke(() => { RefreshAudio(); Changed?.Invoke(); });
        WindowTracker.Instance.ForegroundChanged += () => { if (RefreshLang()) Changed?.Invoke(); };
        RefreshPower(); RefreshLang();
        UpdateClock();
        // Wi-Fi và âm thanh có thể chậm ngay sau khi đăng nhập: đọc nền rồi cập nhật
        Task.Run(() => { var n = Wlan.Query(); d.BeginInvoke(() => { Net = n; Changed?.Invoke(); }); });
        d.BeginInvoke(() => { RefreshAudio(); Changed?.Invoke(); }, DispatcherPriority.ApplicationIdle);
        Config.Changed += s => { if (s is "menubar" or "all") UpdateClock(); };
    }

    public void RefreshAll() { RefreshPower(); RefreshNet(); RefreshAudio(); RefreshLang(); Changed?.Invoke(); }

    void RefreshPower()
    {
        if (Native.GetSystemPowerStatus(out var st))
        {
            bool hasBattery = st.BatteryFlag != 128 && st.BatteryFlag != 255 && st.BatteryLifePercent != 255;
            BatteryPercent = hasBattery ? st.BatteryLifePercent : -1;
            Charging = (st.BatteryFlag & 8) != 0 || (hasBattery && st.ACLineStatus == 1 && st.BatteryLifePercent < 100);
            bool was = OnBattery;
            OnBattery = hasBattery && st.ACLineStatus == 0;
            if (was != OnBattery) App.Instance?.OnPowerChanged();
        }
    }

    void RefreshNet()
    {
        Net = Wlan.Query();
    }

    public void RefreshAudio()
    {
        try
        {
            AudioAvailable = AudioVolume.Instance.Available;
            Volume = AudioVolume.Instance.Level;
            Muted = AudioVolume.Instance.Muted;
        }
        catch { AudioAvailable = false; }
    }

    bool RefreshLang()
    {
        try
        {
            IntPtr fg = Native.GetForegroundWindow();
            uint tid = Native.GetWindowThreadProcessId(fg, out _);
            IntPtr hkl = Native.GetKeyboardLayout(tid);
            if (hkl == InputHkl && InputLang.Length > 0) return false;
            InputHkl = hkl;
            InputLang = LangCode(hkl);
            return true;
        }
        catch { return false; }
    }

    public static string LangCode(IntPtr hkl)
    {
        int langId = (int)(hkl.ToInt64() & 0xFFFF);
        try { return CultureInfo.GetCultureInfo(langId).TwoLetterISOLanguageName.ToUpperInvariant(); }
        catch { return "??"; }
    }

    public static string LangName(IntPtr hkl)
    {
        int langId = (int)(hkl.ToInt64() & 0xFFFF);
        try { var c = CultureInfo.GetCultureInfo(langId); return c.NativeName; }
        catch { return "0x" + langId.ToString("X4"); }
    }

    public static IntPtr[] Layouts()
    {
        int n = Native.GetKeyboardLayoutList(0, null);
        var arr = new IntPtr[Math.Max(n, 0)];
        if (n > 0) Native.GetKeyboardLayoutList(n, arr);
        return arr;
    }

    public void SwitchLayout(IntPtr hkl)
    {
        IntPtr target = WindowTracker.Instance.ForegroundAppHwnd;
        if (target == IntPtr.Zero) target = Native.GetForegroundWindow();
        Native.PostMessage(target, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl);
        InputHkl = IntPtr.Zero;
        Task.Delay(300).ContinueWith(_ => App.Instance.Dispatcher.BeginInvoke(() => { if (RefreshLang()) Changed?.Invoke(); }));
    }

    public string FormatClock(string format, DateTime t)
    {
        try { return t.ToString(format, L.Culture); }
        catch { return t.ToString("HH:mm", L.Culture); }
    }

    void UpdateClock()
    {
        var fmt = Config.Current.MenuBar.ClockFormat;
        if (L.En && fmt == new MenuBarSettings().ClockFormat) fmt = "ddd MMM d   h:mm tt";
        var now = DateTime.Now;
        string text = FormatClock(fmt, now);
        bool seconds = fmt.Contains('s');
        if (_clock != null)
        {
            _clock.Stop();
            _clock.Interval = seconds ? TimeSpan.FromMilliseconds(1000 - now.Millisecond + 5) : TimeSpan.FromSeconds(60 - now.Second).Add(TimeSpan.FromMilliseconds(20 - now.Millisecond));
            if (_clock.Interval < TimeSpan.FromMilliseconds(50)) _clock.Interval = TimeSpan.FromMilliseconds(50);
            _clock.Start();
        }
        if (text != ClockText) { ClockText = text; ClockChanged?.Invoke(); }
        // kiểm tra chế độ sáng/tối theo giờ mỗi phút
        if (!seconds || now.Second == 0) ThemeManager.Apply(false);
    }

    // ---------------- Chế độ tối của Windows ----------------

    public static void SetWindowsDark(bool dark)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", true);
            if (k == null) return;
            k.SetValue("AppsUseLightTheme", dark ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);
            k.SetValue("SystemUsesLightTheme", dark ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);
            IntPtr s = Marshal.StringToHGlobalUni("ImmersiveColorSet");
            try { Native.SendMessageTimeout(Native.HWND_BROADCAST, Native.WM_SETTINGCHANGE, IntPtr.Zero, s, Native.SMTO_ABORTIFHUNG, 200, out _); }
            finally { Marshal.FreeHGlobal(s); }
        }
        catch (Exception ex) { Log.Warn("Đổi chế độ tối lỗi: " + ex.Message); }
    }

    // ---------------- Độ sáng (WMI, laptop) ----------------

    public static int? GetBrightness()
    {
        try
        {
            using var s = new System.Management.ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (System.Management.ManagementObject o in s.Get()) return Convert.ToInt32(o["CurrentBrightness"]);
        }
        catch { }
        return null;
    }

    public static void SetBrightness(int value)
    {
        try
        {
            using var s = new System.Management.ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (System.Management.ManagementObject o in s.Get())
                o.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)Math.Clamp(value, 0, 100) });
        }
        catch (Exception ex) { Log.Debug("Brightness: " + ex.Message); }
    }

    // ---------------- Radio (Wi-Fi / Bluetooth) qua WinRT ----------------

    public static async Task<(bool? wifi, bool? bt)> GetRadios()
    {
        try
        {
            var access = await Windows.Devices.Radios.Radio.RequestAccessAsync();
            if (access != Windows.Devices.Radios.RadioAccessStatus.Allowed) return (null, null);
            var radios = await Windows.Devices.Radios.Radio.GetRadiosAsync();
            bool? wifi = null, bt = null;
            foreach (var r in radios)
            {
                if (r.Kind == Windows.Devices.Radios.RadioKind.WiFi) wifi = r.State == Windows.Devices.Radios.RadioState.On;
                if (r.Kind == Windows.Devices.Radios.RadioKind.Bluetooth) bt = r.State == Windows.Devices.Radios.RadioState.On;
            }
            return (wifi, bt);
        }
        catch (Exception ex) { Log.Debug("Radios: " + ex.Message); return (null, null); }
    }

    public static async Task<bool> SetRadio(Windows.Devices.Radios.RadioKind kind, bool on)
    {
        try
        {
            var radios = await Windows.Devices.Radios.Radio.GetRadiosAsync();
            foreach (var r in radios)
                if (r.Kind == kind)
                {
                    var res = await r.SetStateAsync(on ? Windows.Devices.Radios.RadioState.On : Windows.Devices.Radios.RadioState.Off);
                    return res == Windows.Devices.Radios.RadioAccessStatus.Allowed;
                }
        }
        catch (Exception ex) { Log.Warn("Đổi trạng thái radio lỗi: " + ex.Message); }
        return false;
    }
}
