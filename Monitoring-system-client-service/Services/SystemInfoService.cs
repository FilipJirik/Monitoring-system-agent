using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Monitoring_system_client_service.Services;

public static class SystemInfoService
{
    private const string DefaultIPAddress = "127.0.0.1";
    private const string DefaultMACAddress = "00:00:00:00:00:00";
    private const string DefaultDescription = "Default description";

    private const string x86ArchitecturePath = "/sys/class/dmi/id/product_name";
    private const string ARMArchitecturePath = "/proc/device-tree/model";


    public static string GetLocalIpAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            return host.AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                ?.ToString() ?? DefaultIPAddress;
        }
        catch
        {
            return DefaultIPAddress;
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

            return nic?.GetPhysicalAddress().ToString() ?? DefaultMACAddress;
        }
        catch
        {
            return DefaultMACAddress;
        }
    }

    /// <summary>
    /// Detects if SSH (port 22) is listening by reading /proc/net/tcp.
    /// Each line has local_address in hex as host:port — port 22 = 0016.
    /// Listening state = 0A.
    /// </summary>
    public static bool IsSshEnabled()
    {
        try
        {
            if (!File.Exists("/proc/net/tcp"))
                return false;

            var lines = File.ReadAllLines("/proc/net/tcp");
            foreach (var line in lines.Skip(1)) // skip header
            {
                var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4)
                    continue;

                // parts[1] = local_address (hex:port), parts[3] = state
                var localAddress = parts[1];
                var state = parts[3];

                var portHex = localAddress.Split(':').LastOrDefault();
                if (portHex != null &&
                    int.TryParse(portHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int port) &&
                    port == 22 &&
                    state == "0A") // 0A = LISTEN
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore — can't determine SSH status
        }

        return false;
    }

    /// <summary>
    /// Reads the hardware model from system files.
    /// x86: /sys/class/dmi/id/product_name
    /// ARM: /proc/device-tree/model
    /// </summary>
    public static string? GetHardwareModel()
    {
        string[] paths =
        [
            x86ArchitecturePath,
            ARMArchitecturePath
        ];

        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                string model = File.ReadAllText(path).Trim().TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(model))
                    return model;
            }
            catch
            {
                // Try next path
            }
        }

        return null;
    }

    /// <summary>
    /// Generates a description from the hostname and OS.
    /// </summary>
    public static string GetDeviceDescription()
    {
        try
        {
            string hostname = System.Net.Dns.GetHostName();
            return $"{hostname} - {System.Runtime.InteropServices.RuntimeInformation.OSDescription}";
        }
        catch
        {
            return DefaultDescription;
        }
    }
}
