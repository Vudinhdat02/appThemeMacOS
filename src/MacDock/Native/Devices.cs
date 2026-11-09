using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MacDock;

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator2
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice2 device);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int Item(uint index, out IMMDevice2 device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice2
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore props);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out int state);
}

// IPolicyConfig (không công khai nhưng ổn định từ Windows 7 tới 11) — dùng để đặt thiết bị phát mặc định
[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat(IntPtr a, IntPtr b);
    [PreserveSig] int GetDeviceFormat(IntPtr a, int b, IntPtr c);
    [PreserveSig] int ResetDeviceFormat(IntPtr a);
    [PreserveSig] int SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
    [PreserveSig] int GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
    [PreserveSig] int SetProcessingPeriod(IntPtr a, IntPtr b);
    [PreserveSig] int GetShareMode(IntPtr a, IntPtr b);
    [PreserveSig] int SetShareMode(IntPtr a, IntPtr b);
    [PreserveSig] int GetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
    [PreserveSig] int SetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
    [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
}

[ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
class PolicyConfigCom { }

public sealed record AudioDevice(string Id, string Name, bool IsDefault);

public static class AudioDevices
{
    static readonly PROPERTYKEY PKEY_FriendlyName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

    /// <summary>Thiết bị phát (0) hoặc thu (1) đang hoạt động.</summary>
    public static List<AudioDevice> List(int flow = 0)
    {
        var res = new List<AudioDevice>();
        try
        {
            var en = (IMMDeviceEnumerator2)new MMDeviceEnumeratorCom();
            string? defId = null;
            if (en.GetDefaultAudioEndpoint(flow, 1, out var def) == 0) { def.GetId(out defId); Marshal.ReleaseComObject(def); }
            if (en.EnumAudioEndpoints(flow, 1 /*ACTIVE*/, out var col) != 0) return res;
            col.GetCount(out uint n);
            for (uint i = 0; i < n; i++)
            {
                if (col.Item(i, out var d) != 0) continue;
                d.GetId(out string id);
                string name = id;
                if (d.OpenPropertyStore(0, out var ps) == 0)
                {
                    var key = PKEY_FriendlyName;
                    if (ps.GetValue(ref key, out PROPVARIANT pv) == 0)
                    {
                        if (pv.vt == 31) name = Marshal.PtrToStringUni(pv.pointerValue) ?? id;
                        Native.PropVariantClear(ref pv);
                    }
                    Marshal.ReleaseComObject(ps);
                }
                res.Add(new AudioDevice(id, name, id == defId));
                Marshal.ReleaseComObject(d);
            }
            Marshal.ReleaseComObject(col);
            Marshal.ReleaseComObject(en);
        }
        catch (Exception ex) { Log.Debug("AudioDevices: " + ex.Message); }
        return res;
    }

    public static bool SetDefault(string id)
    {
        try
        {
            var pc = (IPolicyConfig)new PolicyConfigCom();
            int r = 0;
            for (int role = 0; role < 3; role++) r |= pc.SetDefaultEndpoint(id, role);
            Marshal.ReleaseComObject(pc);
            AudioVolume.Instance.Refresh();
            return r == 0;
        }
        catch (Exception ex) { Log.Warn("Đổi thiết bị phát lỗi: " + ex.Message); return false; }
    }

    /// <summary>Tắt/bật micro mặc định. Trả về trạng thái tắt tiếng sau khi đổi (null nếu không có micro).</summary>
    public static bool? MicMuted(bool? set = null)
    {
        try
        {
            var en = (IMMDeviceEnumerator2)new MMDeviceEnumeratorCom();
            if (en.GetDefaultAudioEndpoint(1, 0, out var dev) != 0) return null;
            var iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
            if (dev.Activate(ref iid, 23, IntPtr.Zero, out object o) != 0) return null;
            var ep = (IAudioEndpointVolume)o;
            var ctx = Guid.Empty;
            if (set != null) ep.SetMute(set.Value, ref ctx);
            ep.GetMute(out bool m);
            Marshal.ReleaseComObject(ep); Marshal.ReleaseComObject(dev); Marshal.ReleaseComObject(en);
            return m;
        }
        catch { return null; }
    }
}

public sealed record BtDevice(string Id, string Name, bool Connected);

public static class BluetoothDevices
{
    public static async Task<List<BtDevice>> PairedAsync()
    {
        var res = new List<BtDevice>();
        try
        {
            string[] props = { "System.Devices.Aep.IsConnected" };
            foreach (var sel in new[]
            {
                Windows.Devices.Bluetooth.BluetoothDevice.GetDeviceSelectorFromPairingState(true),
                Windows.Devices.Bluetooth.BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)
            })
            {
                var list = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(sel, props);
                foreach (var d in list)
                {
                    if (string.IsNullOrWhiteSpace(d.Name) || res.Any(x => x.Name == d.Name)) continue;
                    bool c = d.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var v) && v is bool b && b;
                    res.Add(new BtDevice(d.Id, d.Name, c));
                }
            }
        }
        catch (Exception ex) { Log.Debug("Bluetooth: " + ex.Message); }
        return res.OrderByDescending(d => d.Connected).ThenBy(d => d.Name).ToList();
    }
}

public sealed record NowPlayingInfo(string Title, string Artist, string App, bool Playing, ImageSource? Art);

/// <summary>Bài đang phát trong hệ thống (Spotify, trình duyệt, Media Player…) qua System Media Transport Controls.</summary>
public static class NowPlaying
{
    static Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager? _mgr;

    static async Task<Windows.Media.Control.GlobalSystemMediaTransportControlsSession?> Session()
    {
        try
        {
            _mgr ??= await Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            return _mgr.GetCurrentSession();
        }
        catch { return null; }
    }

    public static async Task<NowPlayingInfo?> GetAsync()
    {
        var s = await Session();
        if (s == null) return null;
        try
        {
            var p = await s.TryGetMediaPropertiesAsync();
            var pb = s.GetPlaybackInfo();
            ImageSource? art = null;
            if (p.Thumbnail != null)
            {
                try
                {
                    using var ras = await p.Thumbnail.OpenReadAsync();
                    using var st = ras.AsStreamForRead();
                    var ms = new MemoryStream(); await st.CopyToAsync(ms); ms.Position = 0;
                    var bi = new BitmapImage(); bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.DecodePixelWidth = 160; bi.StreamSource = ms; bi.EndInit(); bi.Freeze();
                    art = bi;
                }
                catch { }
            }
            string app = s.SourceAppUserModelId ?? "";
            return new NowPlayingInfo(p.Title ?? "", p.Artist ?? "", app,
                pb.PlaybackStatus == Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing, art);
        }
        catch { return null; }
    }

    public static async Task Toggle() { var s = await Session(); if (s != null) await s.TryTogglePlayPauseAsync(); }
    public static async Task Next() { var s = await Session(); if (s != null) await s.TrySkipNextAsync(); }
    public static async Task Previous() { var s = await Session(); if (s != null) await s.TrySkipPreviousAsync(); }
}
