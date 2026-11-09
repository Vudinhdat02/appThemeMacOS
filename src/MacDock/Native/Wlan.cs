using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace MacDock;

public enum NetKind { None, Wifi, Ethernet }

public record NetStatus(NetKind Kind, string Name, int SignalPercent);

/// <summary>Đọc trạng thái Wi-Fi qua Native Wifi API (wlanapi.dll).</summary>
public static class Wlan
{
    [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(uint ver, IntPtr reserved, out uint negotiated, out IntPtr handle);
    [DllImport("wlanapi.dll")] static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern int WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);
    [DllImport("wlanapi.dll")] static extern int WlanQueryInterface(IntPtr handle, ref Guid iface, int opcode, IntPtr reserved, out int size, out IntPtr data, out int valueType);
    [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr p);

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
