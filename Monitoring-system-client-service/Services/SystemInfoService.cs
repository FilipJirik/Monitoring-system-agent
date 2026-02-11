using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Monitoring_system_client_service.Services;

public static class SystemInfoService
{
    public static string GetLocalIpAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            return host.AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                ?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    public static string GetMacAddress()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                           n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(n => n.Speed)
                .FirstOrDefault();

            return nic?.GetPhysicalAddress().ToString() ?? "00:00:00:00:00:00";
        }
        catch
        {
            return "00:00:00:00:00:00";
        }
    }
}

