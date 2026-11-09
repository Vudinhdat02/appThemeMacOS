using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace MacDock;

public enum NetKind { None, Wifi, Ethernet }

public record NetStatus(NetKind Kind, string Name, int SignalPercent);

public sealed record WifiNetwork(string Ssid, int Signal, bool Secured, bool Connected, string? Profile, int Auth, Guid Interface);

/// <summary>Đọc trạng thái Wi-Fi qua Native Wifi API (wlanapi.dll).</summary>
public static class Wlan
{
    [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(uint ver, IntPtr reserved, out uint negotiated, out IntPtr handle);
    [DllImport("wlanapi.dll")] static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern int WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);
    [DllImport("wlanapi.dll")] static extern int WlanQueryInterface(IntPtr handle, ref Guid iface, int opcode, IntPtr reserved, out int size, out IntPtr data, out int valueType);
    [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr p);

    [DllImport("wlanapi.dll")] static extern int WlanGetAvailableNetworkList(IntPtr handle, ref Guid iface, int flags, IntPtr reserved, out IntPtr list);
    [DllImport("wlanapi.dll")] static extern int WlanScan(IntPtr handle, ref Guid iface, IntPtr ssid, IntPtr ie, IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern int WlanConnect(IntPtr handle, ref Guid iface, ref WLAN_CONNECTION_PARAMETERS p, IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern int WlanDisconnect(IntPtr handle, ref Guid iface, IntPtr reserved);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)] static extern int WlanSetProfile(IntPtr handle, ref Guid iface, int flags, string xml, string? security, bool overwrite, IntPtr reserved, out int reason);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WLAN_CONNECTION_PARAMETERS
    {
        public int wlanConnectionMode;
        [MarshalAs(UnmanagedType.LPWStr)] public string strProfile;
        public IntPtr pDot11Ssid, pDesiredBssidList;
        public int dot11BssType;
        public int dwFlags;
    }

    static List<Guid> Interfaces(IntPtr h)
    {
        var res = new List<Guid>();
        if (WlanEnumInterfaces(h, IntPtr.Zero, out IntPtr list) != 0) return res;
        try
        {
            int count = Marshal.ReadInt32(list, 0);
            for (int i = 0; i < count; i++) res.Add(Marshal.PtrToStructure<Guid>(list + 8 + i * 532));
        }
        finally { WlanFreeMemory(list); }
        return res;
    }

    /// <summary>Danh sách mạng Wi-Fi xung quanh (gộp theo SSID, mạnh nhất trước).</summary>
    public static List<WifiNetwork> Networks(bool scan = false)
    {
        var result = new List<WifiNetwork>();
        try
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out IntPtr h) != 0) return result;
            try
            {
                foreach (var g0 in Interfaces(h))
                {
                    var g = g0;
                    if (scan) { WlanScan(h, ref g, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero); Thread.Sleep(1500); }
                    if (WlanGetAvailableNetworkList(h, ref g, 0, IntPtr.Zero, out IntPtr list) != 0) continue;
                    try
                    {
                        int n = Marshal.ReadInt32(list, 0);
                        for (int i = 0; i < n; i++)
                        {
                            IntPtr it = list + 8 + i * 628;
                            string profile = Marshal.PtrToStringUni(it, 256).Split((char)0)[0];
                            int len = Math.Clamp(Marshal.ReadInt32(it, 512), 0, 32);
                            var b = new byte[len]; Marshal.Copy(it + 516, b, 0, len);
                            string ssid = Encoding.UTF8.GetString(b);
                            if (ssid.Length == 0) continue;
                            int signal = Marshal.ReadInt32(it, 604);
                            bool secured = Marshal.ReadInt32(it, 608) != 0;
                            int auth = Marshal.ReadInt32(it, 612);
                            int flags = Marshal.ReadInt32(it, 620);
                            result.Add(new WifiNetwork(ssid, signal, secured, (flags & 1) != 0, (flags & 2) != 0 && profile.Length > 0 ? profile : null, auth, g));
                        }
                    }
                    finally { WlanFreeMemory(list); }
                }
            }
            finally { WlanCloseHandle(h, IntPtr.Zero); }
        }
        catch (Exception ex) { Log.Debug("Wlan list: " + ex.Message); }
        return result.GroupBy(r => r.Ssid)
            .Select(gr => gr.OrderByDescending(x => x.Connected).ThenByDescending(x => x.Profile != null).ThenByDescending(x => x.Signal).First() with { Signal = gr.Max(x => x.Signal) })
            .OrderByDescending(r => r.Connected).ThenByDescending(r => r.Signal).ToList();
    }

    static string Esc(string s) => System.Security.SecurityElement.Escape(s) ?? s;

    /// <summary>Kết nối tới mạng. Mạng chưa có hồ sơ thì tạo hồ sơ với mật khẩu (nếu có). Trả về thông báo lỗi hoặc null.</summary>
    public static string? Connect(WifiNetwork net, string? password)
    {
        try
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out IntPtr h) != 0) return "Không mở được Wi-Fi";
            try
            {
                var g = net.Interface;
                string profile = net.Profile ?? net.Ssid;
                if (net.Profile == null || password != null)
                {
                    string auth = net.Auth switch { 9 or 10 => "WPA3SAE", 4 => "WPAPSK", 1 => "open", _ => "WPA2PSK" };
                    string security = !net.Secured
                        ? "<authEncryption><authentication>open</authentication><encryption>none</encryption><useOneX>false</useOneX></authEncryption>"
                        : $"<authEncryption><authentication>{auth}</authentication><encryption>AES</encryption><useOneX>false</useOneX></authEncryption><sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>{Esc(password ?? "")}</keyMaterial></sharedKey>";
                    string hex = Convert.ToHexString(Encoding.UTF8.GetBytes(net.Ssid));
                    string xml = $"<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>{Esc(net.Ssid)}</name><SSIDConfig><SSID><hex>{hex}</hex><name>{Esc(net.Ssid)}</name></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>auto</connectionMode><MSM><security>{security}</security></MSM></WLANProfile>";
                    int r = WlanSetProfile(h, ref g, 0, xml, null, true, IntPtr.Zero, out int reason);
                    if (r != 0) return $"Không lưu được hồ sơ mạng (mã {r}/{reason})";
                    profile = net.Ssid;
                }
                var p = new WLAN_CONNECTION_PARAMETERS { wlanConnectionMode = 0, strProfile = profile, dot11BssType = 1 };
                int rc = WlanConnect(h, ref g, ref p, IntPtr.Zero);
                return rc == 0 ? null : $"Không kết nối được (mã {rc})";
            }
            finally { WlanCloseHandle(h, IntPtr.Zero); }
        }
        catch (Exception ex) { return ex.Message; }
    }

    public static void Disconnect(WifiNetwork net)
    {
        try
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out IntPtr h) != 0) return;
            var g = net.Interface;
            WlanDisconnect(h, ref g, IntPtr.Zero);
            WlanCloseHandle(h, IntPtr.Zero);
        }
        catch { }
    }

    const int wlan_intf_opcode_current_connection = 7;
    const int wlan_interface_state_connected = 1;

    public static NetStatus Query()
    {
        try
        {
            var w = QueryWifi();
            if (w != null) return w;
        }
        catch (DllNotFoundException) { }
        catch (Exception ex) { Log.Debug("Wlan: " + ex.Message); }

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT)
                {
                    if (ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) || ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)) continue;
                    if (ni.GetIPProperties().GatewayAddresses.Count == 0) continue;
                    return new NetStatus(NetKind.Ethernet, ni.Name, 100);
                }
            }
        }
        catch { }
        return new NetStatus(NetKind.None, "", 0);
    }

    static NetStatus? QueryWifi()
    {
        if (WlanOpenHandle(2, IntPtr.Zero, out _, out IntPtr h) != 0) return null;
        try
        {
            if (WlanEnumInterfaces(h, IntPtr.Zero, out IntPtr list) != 0) return null;
            try
            {
                int count = Marshal.ReadInt32(list, 0);
                for (int i = 0; i < count; i++)
                {
                    IntPtr item = list + 8 + i * 532;
                    var guid = Marshal.PtrToStructure<Guid>(item);
                    int state = Marshal.ReadInt32(item, 16 + 512);
                    if (state != wlan_interface_state_connected) continue;
                    if (WlanQueryInterface(h, ref guid, wlan_intf_opcode_current_connection, IntPtr.Zero, out _, out IntPtr data, out _) != 0) continue;
                    try
                    {
                        int ssidLen = Marshal.ReadInt32(data, 520);
                        var bytes = new byte[Math.Clamp(ssidLen, 0, 32)];
                        Marshal.Copy(data + 524, bytes, 0, bytes.Length);
                        string ssid = Encoding.UTF8.GetString(bytes);
                        int quality = Marshal.ReadInt32(data, 576);
                        return new NetStatus(NetKind.Wifi, ssid, Math.Clamp(quality, 0, 100));
                    }
                    finally { WlanFreeMemory(data); }
                }
            }
            finally { WlanFreeMemory(list); }
        }
        finally { WlanCloseHandle(h, IntPtr.Zero); }
        return null;
    }
}
