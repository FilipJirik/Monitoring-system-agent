using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Monitoring_system_client_service.Services;

/// <summary>
/// Service for retrieving system and device information.
/// </summary>
public static class SystemInfoService
{
    /// <summary>
    /// Gets the hostname from the DNS server.
    /// </summary>
    /// <returns>Hostname</returns>
    public static string GetHostName() => System.Net.Dns.GetHostName();

    /// <summary>
    /// Gets the operating system description.
    /// </summary>
    /// <returns>OS description</returns>
    public static string GetOsDescription() => RuntimeInformation.OSDescription;

    /// <summary>
    /// Retrieves the local IPv4 address of the device.
    /// </summary>
    /// <returns>The local IPv4 address, or "127.0.0.1" if unable to determine</returns>
    public static string GetLocalIpAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch
        {
            // Fallback to localhost if unable to determine
        }
        return "127.0.0.1";
    }

    /// <summary>
    /// Retrieves the MAC address of the primary network interface.
    /// </summary>
    /// <returns>The MAC address in string format, or "00:00:00:00:00:00" if unable to determine</returns>
    public static string GetMacAddress()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(n => n.Speed)
                .FirstOrDefault();

            if (nic != null) 
                return nic.GetPhysicalAddress().ToString();
        }
        catch
        {
            // Fallback to placeholder if unable to determine
        }
        return "00:00:00:00:00:00";
    }
}

