using System.Runtime.InteropServices;

namespace MacDock;

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback cb);
    [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback cb);
    [PreserveSig] int GetChannelCount(out uint count);
    [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid ctx);
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
    [PreserveSig] int GetMasterVolumeLevel(out float db);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    [PreserveSig] int SetChannelVolumeLevel(uint ch, float db, ref Guid ctx);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
    [PreserveSig] int GetChannelVolumeLevel(uint ch, out float db);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint ch, out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

[ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolumeCallback
{
    [PreserveSig] int OnNotify(IntPtr pNotify);
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
class MMDeviceEnumeratorCom { }

/// <summary>Điều khiển âm lượng loa mặc định qua Core Audio.</summary>
public sealed class AudioVolume
{
    public static AudioVolume Instance { get; } = new();

    IAudioEndpointVolume? _ep;
    Callback? _cb;
    Guid _ctx = Guid.NewGuid();
    DateTime _acquired;

    /// <summary>Âm lượng thay đổi (có thể gọi từ luồng khác).</summary>
    public event Action? Changed;

    sealed class Callback : IAudioEndpointVolumeCallback
    {
        readonly AudioVolume _owner;
        public Callback(AudioVolume o) { _owner = o; }
        public int OnNotify(IntPtr p) { try { _owner.Changed?.Invoke(); } catch { } return 0; }
    }

    IAudioEndpointVolume? Endpoint()
    {
        if (_ep != null && (DateTime.Now - _acquired).TotalSeconds < 30) return _ep;
        try
        {
            Release();
            var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            if (en.GetDefaultAudioEndpoint(0, 1, out var dev) != 0 || dev == null) return null;
            var iid = typeof(IAudioEndpointVolume).GUID;
            if (dev.Activate(ref iid, 23, IntPtr.Zero, out object o) != 0) return null;
            _ep = (IAudioEndpointVolume)o;
            _cb = new Callback(this);
            _ep.RegisterControlChangeNotify(_cb);
            _acquired = DateTime.Now;
            Marshal.ReleaseComObject(dev);
            Marshal.ReleaseComObject(en);
        }
        catch (Exception ex)
        {
            Log.Debug("Audio: " + ex.Message);
            _ep = null;
        }
        return _ep;
    }

    void Release()
    {
        try
        {
            if (_ep != null && _cb != null) _ep.UnregisterControlChangeNotify(_cb);
            if (_ep != null) Marshal.ReleaseComObject(_ep);
        }
        catch { }
        _ep = null; _cb = null;
    }

    public bool Available => Endpoint() != null;

    public float Level
    {
        get { var e = Endpoint(); if (e == null) return 0; e.GetMasterVolumeLevelScalar(out float v); return v; }
        set { var e = Endpoint(); if (e == null) return; e.SetMasterVolumeLevelScalar(Math.Clamp(value, 0f, 1f), ref _ctx); }
    }

    public bool Muted
    {
        get { var e = Endpoint(); if (e == null) return false; e.GetMute(out bool m); return m; }
        set { var e = Endpoint(); e?.SetMute(value, ref _ctx); }
    }

    /// <summary>Gọi định kỳ để giữ kết nối với thiết bị mặc định (khi người dùng đổi loa).</summary>
    public void Refresh() { _acquired = DateTime.MinValue; Endpoint(); }
}
