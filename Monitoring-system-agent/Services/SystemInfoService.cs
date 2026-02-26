using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Monitoring_system_client_service.Services;

/// <summary>
/// Provides system information retrieval capabilities for the monitoring agent.
/// Safely detects local IP address, MAC address, hardware model, and SSH availability.
/// </summary>
public static class SystemInfoService
{
    // Default fallback values
    private const string DefaultIpAddress = "127.0.0.1";
    private const string DefaultMacAddress = "00:00:00:00:00:00";
    private const string DefaultDescription = "Default description";

    // Linux system file paths
    private const string X86HardwareModelPath = "/sys/class/dmi/id/product_name";
    private const string ArmHardwareModelPath = "/proc/device-tree/model";
    private const string ProcNetTcpPath = "/proc/net/tcp";

    // Network detection constants
    private const string InternetRoutingIp = "8.8.8.8";
    private const int DummyRoutingPort = 65530;
    private const int SocketConnectTimeoutMs = 1000;

    // /proc/net/tcp parsing constants
    private const int TcpLineMinFields = 4;
    private const int TcpLocalAddressFieldIndex = 1;
    private const int TcpStateFieldIndex = 3;
    private const int SshPortNumber = 22;
    private const string TcpListeningStateHex = "0A";
    private const int TcpHeaderSkipLines = 1;

    /// <summary>
    /// Retrieves the local IPv4 address used for outbound network communication.
    /// 
    /// This method safely determines the preferred outbound interface by querying the OS routing table
    /// using a UDP socket connection to 8.8.8.8:65530 (no actual traffic is sent).
    /// This approach avoids issues with Dns.GetHostEntry() returning loopback addresses in
    /// containerized or virtualized environments (Linux, Docker, VirtualBox).
    /// </summary>
    /// <returns>
    /// The IPv4 address of the preferred outbound interface, or "127.0.0.1" if detection fails.
    /// </returns>
    public static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(InternetRoutingIp, DummyRoutingPort);

            if (socket.LocalEndPoint is IPEndPoint localEndPoint)
            {
                string address = localEndPoint.Address.ToString();
                return !string.IsNullOrEmpty(address) ? address : DefaultIpAddress;
            }

            return DefaultIpAddress;
        }
        catch (SocketException)
        {
            // Network interface unavailable or socket operation failed - fall back to loopback for configuration
            return DefaultIpAddress;
        }
        catch (Exception)
        {
            // Unexpected error during socket operations - configuration can still proceed with fallback address
            return DefaultIpAddress;
        }
    }

    /// <summary>
    /// Retrieves the MAC address of the highest-speed active network interface.
    /// </summary>
    /// <returns>
    /// The MAC address in colon-separated hexadecimal format (e.g., "AA:BB:CC:DD:EE:FF"),
    /// or "00:00:00:00:00:00" if no suitable interface is found.
    /// </returns>
    public static string GetMacAddress()
    {
        try
        {
            var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                             nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(nic => nic.Speed)
                .FirstOrDefault();

            if (activeInterface == null)
                return DefaultMacAddress;

            byte[] macAddressBytes = activeInterface.GetPhysicalAddress().GetAddressBytes();

            if (macAddressBytes.Length == 0)
                return DefaultMacAddress;

            return string.Join(":", macAddressBytes.Select(b => b.ToString("X2")));
        }
        catch (NetworkInformationException)
        {
            // Network information unavailable on this platform - use default placeholder
            return DefaultMacAddress;
        }
        catch (Exception)
        {
            // Unexpected error during network interface enumeration - configuration can proceed with fallback value
            return DefaultMacAddress;
        }
    }

    /// <summary>
    /// Detects whether SSH (Secure Shell) is enabled and listening on the default port (22).
    /// 
    /// This method parses the Linux /proc/net/tcp file to identify listening sockets.
    /// Each line contains the local address in hexadecimal format (host:port), where port 22 is 0x0016.
    /// The state field value "0A" indicates the LISTEN state.
    /// </summary>
    /// <returns>
    /// True if SSH is detected as listening on port 22; otherwise, false.
    /// Returns false if /proc/net/tcp is unavailable or parsing fails.
    /// </returns>
    public static bool IsSshEnabled()
    {
        try
        {
            if (!File.Exists(ProcNetTcpPath))
                return false;

            var lines = File.ReadAllLines(ProcNetTcpPath);
            foreach (var line in lines.Skip(TcpHeaderSkipLines))
            {
                var fields = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (fields.Length < TcpLineMinFields)
                    continue;

                string localAddressField = fields[TcpLocalAddressFieldIndex];
                string stateField = fields[TcpStateFieldIndex];

                string? portHexadecimal = localAddressField.Split(':').LastOrDefault();

                if (portHexadecimal != null &&
                    int.TryParse(portHexadecimal, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int port) &&
                    port == SshPortNumber &&
                    stateField == TcpListeningStateHex)
                {
                    return true;
                }
            }
        }
        catch (IOException)
        {
            // Cannot read /proc/net/tcp - likely not on Linux or file is inaccessible
            return false;
        }
        catch (Exception)
        {
            // Unexpected error during SSH detection - safely assume SSH is not available
            return false;
        }

        return false;
    }

    /// <summary>
    /// Retrieves the hardware model/product name from system files.
    /// 
    /// Attempts to read from multiple platform-specific paths:
    /// - x86/x64: /sys/class/dmi/id/product_name
    /// - ARM: /proc/device-tree/model
    /// </summary>
    /// <returns>
    /// The hardware model name if available; otherwise, null.
    /// </returns>
    public static string? GetHardwareModel()
    {
        var hardwarePaths = new[] { X86HardwareModelPath, ArmHardwareModelPath };

        foreach (var path in hardwarePaths)
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                string model = File.ReadAllText(path).Trim().TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(model))
                    return model;
            }
            catch (IOException)
            {
                // File not accessible on this system - try next path
                continue;
            }
            catch (Exception)
            {
                // Unexpected error reading hardware model - try next path or return null
                continue;
            }
        }

        return null;
    }

    /// <summary>
    /// Generates a human-readable description of the device from hostname and operating system information.
    /// </summary>
    /// <returns>
    /// A formatted string containing the hostname and OS description (e.g., "myhost - Linux 5.10.0"),
    /// or a default description if retrieval fails.
    /// </returns>
    public static string GetDeviceDescription()
    {
        try
        {
            string hostname = Dns.GetHostName();
            string osDescription = RuntimeInformation.OSDescription;

            return $"{hostname} - {osDescription}";
        }
        catch (Exception)
        {
            // Unable to retrieve hostname or OS description - use generic fallback description
            return DefaultDescription;
        }
    }
}
