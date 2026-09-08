using Android.Net;
using Java.Net;
using Application = Android.App.Application;
using NetworkInterface = Java.Net.NetworkInterface;

namespace CibMedia.AndroidTv.Platform;

// Read through Android and Java.Net: System.Net.NetworkInformation reports nothing but loopback
// on Android.
public static class DeviceAddress
{
    // Wi-Fi Direct owns this range, and a box that mirrors screens keeps p2p0 up with it.
    private const string WifiDirectPrefix = "192.168.49.";

    // A phone on the Wi-Fi reaches the box on the box's Wi-Fi address, or on its Ethernet
    // address when the box is wired into the same router. What Android routes through is only
    // consulted for a box whose interfaces carry other names, and never when that is a VPN.
    public static string? LocalIPv4()
    {
        var interfaces = UpInterfaces();

        return AddressOn(interfaces, "wlan") ?? AddressOn(interfaces, "eth") ?? ActiveNetworkIPv4();
    }

    private static string? AddressOn(List<NetworkInterface> interfaces, string namePrefix)
    {
        foreach (var adapter in interfaces)
        {
            if (adapter.Name?.StartsWith(namePrefix, StringComparison.Ordinal) != true) continue;
            if (FirstReachable(adapter) is { } ip) return ip;
        }

        return null;
    }

    private static List<NetworkInterface> UpInterfaces()
    {
        var result = new List<NetworkInterface>();

        try
        {
            var interfaces = NetworkInterface.NetworkInterfaces;

            while (interfaces?.HasMoreElements == true)
            {
                if (interfaces.NextElement() is NetworkInterface { IsLoopback: false, IsUp: true } adapter)
                    result.Add(adapter);
            }
        }
        catch (Java.Lang.Exception)
        {
        }

        return result;
    }

    private static string? FirstReachable(NetworkInterface adapter)
    {
        try
        {
            var addresses = adapter.InetAddresses;

            while (addresses?.HasMoreElements == true)
            {
                if (addresses.NextElement() is InetAddress address && Reachable(address) is { } ip) return ip;
            }
        }
        catch (Java.Lang.Exception)
        {
        }

        return null;
    }

    private static string? ActiveNetworkIPv4()
    {
        try
        {
            if (Application.Context.GetSystemService(Android.Content.Context.ConnectivityService)
                is not ConnectivityManager connectivity) return null;

            if (connectivity.ActiveNetwork is not { } network) return null;
            if (connectivity.GetNetworkCapabilities(network)?.HasTransport(TransportType.Vpn) == true) return null;

            return connectivity.GetLinkProperties(network)?.LinkAddresses?
                .Select(link => Reachable(link.Address))
                .FirstOrDefault(ip => ip is not null);
        }
        catch (Java.Lang.Exception)
        {
            return null;
        }
    }

    private static string? Reachable(InetAddress? address)
    {
        if (address is not Inet4Address || address.IsLoopbackAddress || address.IsLinkLocalAddress) return null;

        var ip = address.HostAddress;

        return ip is null || ip.StartsWith(WifiDirectPrefix, StringComparison.Ordinal) ? null : ip;
    }
}
