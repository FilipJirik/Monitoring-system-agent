using Monitoring_system_client_service.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;

namespace Monitoring_system_client_service.Services;

/// <summary>
/// Collects system metrics from Linux /proc and /sys filesystems.
/// Provides CPU usage, frequency, temperature, memory, disk, and network throughput metrics.
/// </summary>
public class LinuxMetricsService
{
    private readonly ILogger<LinuxMetricsService> _logger;

    // Previous-sample state for delta calculations
    private long _prevCpuTotalTicks;
    private long _prevCpuIdleTicks;
    private long _prevNetInBytes;
    private long _prevNetOutBytes;
    private DateTimeOffset _lastNetworkCollectionTime = DateTimeOffset.MinValue;

    // Linux /proc and /sys filesystem paths
    private const string ProcStatPath = "/proc/stat";
    private const string ProcCpuInfoPath = "/proc/cpuinfo";
    private const string ProcMemInfoPath = "/proc/meminfo";
    private const string ProcUptimePath = "/proc/uptime";
    private const string ProcNetDevPath = "/proc/net/dev";
    private const string ThermalZoneBasePathTemplate = "/sys/class/thermal/thermal_zone";

    // /proc/stat parsing constants
    private const int CpuLineIdleFieldIndex = 4;
    private const int CpuLineIowaitFieldIndex = 5;
    private const int CpuLineTotalFieldsToSum = 7;
    private const int CpuLineSkipFirstField = 1;

    // /proc/cpuinfo parsing constants
    private const string CpuMhzLinePrefix = "cpu MHz";
    private const string BogoMipsLinePrefix = "BogoMIPS";

    // /proc/meminfo parsing constants
    private const string MemTotalKeyword = "MemTotal:";
    private const string MemAvailableKeyword = "MemAvailable:";
    private const int KiloBytesPerMegaByte = 1024;

    // Thermal zone constants
    private const int MaxThermalZonesToCheck = 10;

    // /proc/net/dev parsing constants
    private const int ProcNetDevHeaderLinesToSkip = 2;
    private const int NetworkRxBytesFieldIndex = 1;
    private const int NetworkTxBytesFieldIndex = 9;
    private const int NetworkLineMinimumFields = 10;
    private const string LoopbackInterfaceName = "lo";

    // Network throughput conversion constants
    private const double MillidegreesPerCelsius = 1000.0;
    private const double BitsPerByte = 8.0;
    private const double KiloBitsPerMegabit = 1000.0;
    private const double BytesToKilobitsPerSecond = 0.008; // (8 bits/byte) / 1000 (bits/kilobit)
    private const double MinimumNetworkSamplingIntervalSeconds = 1.0;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinuxMetricsService"/> class.
    /// </summary>
    /// <param name="logger">Logger for recording metric collection operations.</param>
    public LinuxMetricsService(ILogger<LinuxMetricsService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Collects all system metrics asynchronously.
    /// </summary>
    /// <returns>
    /// A <see cref="MetricsModel"/> containing collected system metrics.
    /// Some metrics may be null if they cannot be collected or are unavailable.
    /// </returns>
    public async Task<MetricsModel> GetMetricsAsync()
    {
        var metrics = new MetricsModel
        {
            Timestamp = DateTimeOffset.UtcNow,
            UptimeSeconds = CollectUptime()
        };

        await CollectSafeAsync(() => CollectCpuUsageAsync(metrics), "CPU usage");
        await CollectSafeAsync(() => CollectCpuFrequencyAsync(metrics), "CPU frequency");
        await CollectSafeAsync(() => CollectCpuTemperatureAsync(metrics), "CPU temperature");
        await CollectSafeAsync(() => CollectMemoryUsageAsync(metrics), "memory usage");
        CollectSafe(() => CollectDiskUsage(metrics), "disk usage");
        await CollectSafeAsync(() => CollectNetworkThroughputAsync(metrics), "network throughput");
        CollectSafe(() => CollectProcessCount(metrics), "process count");
        CollectSafe(() => CollectTcpMetrics(metrics), "TCP metrics");

        return metrics;
    }

    /// <summary>
    /// Collects CPU usage percentage from /proc/stat.
    /// Uses delta calculation between consecutive samples.
    /// </summary>
    private async Task CollectCpuUsageAsync(MetricsModel metrics)
    {
        if (!File.Exists(ProcStatPath)) return;

        string firstLine = (await File.ReadAllLinesAsync(ProcStatPath))[0];
        var fields = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // /proc/stat format: cpu user nice system idle iowait irq softirq
        long idle = long.Parse(fields[CpuLineIdleFieldIndex]);
        long iowait = long.Parse(fields[CpuLineIowaitFieldIndex]);
        long currentIdle = idle + iowait;
        long currentTotal = fields.Skip(CpuLineSkipFirstField)
            .Take(CpuLineTotalFieldsToSum)
            .Sum(field => long.TryParse(field, out long value) ? value : 0);

        if (_prevCpuTotalTicks > 0)
        {
            long deltaTotal = currentTotal - _prevCpuTotalTicks;
            long deltaIdle = currentIdle - _prevCpuIdleTicks;

            if (deltaTotal > 0)
                metrics.CpuUsagePercent = Math.Round((1.0 - (double)deltaIdle / deltaTotal) * 100.0, 2);
        }

        _prevCpuTotalTicks = currentTotal;
        _prevCpuIdleTicks = currentIdle;
    }

    /// <summary>
    /// Collects average CPU frequency (MHz) from /proc/cpuinfo.
    /// </summary>
    private async Task CollectCpuFrequencyAsync(MetricsModel metrics)
    {
        if (!File.Exists(ProcCpuInfoPath)) return;

        var lines = await File.ReadAllLinesAsync(ProcCpuInfoPath);
        double totalMhz = 0;
        int coreCount = 0;

        foreach (var line in lines)
        {
            // "cpu MHz" on x86, "BogoMIPS" as ARM fallback
            if (!line.StartsWith(CpuMhzLinePrefix) && !line.StartsWith(BogoMipsLinePrefix))
                continue;

            var parts = line.Split(':');
            if (parts.Length > 1 &&
                double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double mhz))
            {
                totalMhz += mhz;
                coreCount++;
            }
        }

        if (coreCount > 0)
            metrics.CpuFreqAvgMhz = (long)(totalMhz / coreCount);
    }

    /// <summary>
    /// Collects CPU temperature from /sys/class/thermal thermal zone files.
    /// Attempts to find a CPU-specific thermal sensor, falls back to thermal_zone0.
    /// </summary>
    private async Task CollectCpuTemperatureAsync(MetricsModel metrics)
    {
        // Search thermal zones for a CPU-related sensor
        for (int i = 0; i < MaxThermalZonesToCheck; i++)
        {
            string tempPath = $"{ThermalZoneBasePathTemplate}{i}/temp";
            string typePath = $"{ThermalZoneBasePathTemplate}{i}/type";

            if (!File.Exists(tempPath))
                continue;

            try
            {
                string zoneType = File.Exists(typePath) ? await File.ReadAllTextAsync(typePath) : "";

                bool isCpuSensor = zoneType.Contains("cpu", StringComparison.OrdinalIgnoreCase)
                                || zoneType.Contains("pkg", StringComparison.OrdinalIgnoreCase)
                                || zoneType.Contains("temp", StringComparison.OrdinalIgnoreCase);

                if (!isCpuSensor)
                    continue;

                string tempContent = await File.ReadAllTextAsync(tempPath);
                if (TryReadTemperature(tempContent, out double celsius))
                {
                    metrics.CpuTempCelsius = celsius;
                    return;
                }
            }
            catch
            {
                // Try next thermal zone
                continue;
            }
        }

        // Fallback: thermal_zone0
        string fallbackPath = $"{ThermalZoneBasePathTemplate}0/temp";
        if (metrics.CpuTempCelsius == null && File.Exists(fallbackPath))
        {
            try
            {
                string tempContent = await File.ReadAllTextAsync(fallbackPath);
                if (TryReadTemperature(tempContent, out double celsius))
                    metrics.CpuTempCelsius = celsius;
            }
            catch
            {
                // Unable to read fallback temperature
            }
        }
    }

    /// <summary>
    /// Attempts to parse a temperature value from millidegrees Celsius string to Celsius.
    /// </summary>
    /// <param name="rawTemperature">The raw temperature string in millidegrees.</param>
    /// <param name="celsius">Output: temperature in Celsius, rounded to 1 decimal place.</param>
    /// <returns>True if parsing was successful; false otherwise.</returns>
    private static bool TryReadTemperature(string rawTemperature, out double celsius)
    {
        celsius = 0;
        if (!double.TryParse(rawTemperature.Trim(), out double millidegrees))
            return false;

        celsius = Math.Round(millidegrees / MillidegreesPerCelsius, 1);
        return true;
    }

    /// <summary>
    /// Collects memory usage (RAM) from /proc/meminfo.
    /// </summary>
    private async Task CollectMemoryUsageAsync(MetricsModel metrics)
    {
        if (!File.Exists(ProcMemInfoPath)) return;

        var lines = await File.ReadAllLinesAsync(ProcMemInfoPath);
        long totalKb = 0;
        long availableKb = 0;

        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            switch (parts[0])
            {
                case MemTotalKeyword:
                    totalKb = long.Parse(parts[1]);
                    break;
                case MemAvailableKeyword:
                    availableKb = long.Parse(parts[1]);
                    break;
            }
        }

        if (totalKb > 0)
            metrics.RamUsageMb = (totalKb - availableKb) / KiloBytesPerMegaByte;
    }

    /// <summary>
    /// Collects disk usage percentage for the root filesystem.
    /// </summary>
    private static void CollectDiskUsage(MetricsModel metrics)
    {
        var rootDrive = DriveInfo.GetDrives()
            .FirstOrDefault(drive => drive.Name == "/" && drive.IsReady);

        if (rootDrive == null) return;

        double total = rootDrive.TotalSize;
        double free = rootDrive.AvailableFreeSpace;
        metrics.DiskUsagePercent = Math.Round((total - free) / total * 100.0, 2);
    }

    /// <summary>
    /// Collects network throughput (bytes per second) from /proc/net/dev.
    /// Uses delta calculation between consecutive samples, requires minimum 1-second interval.
    /// </summary>
    private async Task CollectNetworkThroughputAsync(MetricsModel metrics)
    {
        if (!File.Exists(ProcNetDevPath)) return;

        var lines = await File.ReadAllLinesAsync(ProcNetDevPath);
        long currentIn = 0;
        long currentOut = 0;

        // Skip header lines
        foreach (var line in lines.Skip(ProcNetDevHeaderLinesToSkip))
        {
            var fields = line.Trim().Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length < NetworkLineMinimumFields || fields[0] == LoopbackInterfaceName)
                continue;

            // fields: [interface, rx_bytes, rx_packets, ..., tx_bytes, ...]
            if (long.TryParse(fields[NetworkRxBytesFieldIndex], out long rx)) currentIn += rx;
            if (long.TryParse(fields[NetworkTxBytesFieldIndex], out long tx)) currentOut += tx;
        }

        var now = DateTimeOffset.UtcNow;

        // Only calculate throughput if we have a previous sample and enough time has elapsed
        if (_lastNetworkCollectionTime != DateTimeOffset.MinValue)
        {
            double elapsedSeconds = (now - _lastNetworkCollectionTime).TotalSeconds;

            if (elapsedSeconds >= MinimumNetworkSamplingIntervalSeconds)
            {
                long deltaIn = Math.Max(0, currentIn - _prevNetInBytes);
                long deltaOut = Math.Max(0, currentOut - _prevNetOutBytes);

                // Convert bytes to kilobits per second
                metrics.NetworkInKbps = Math.Round((deltaIn / elapsedSeconds) * BytesToKilobitsPerSecond, 2);
                metrics.NetworkOutKbps = Math.Round((deltaOut / elapsedSeconds) * BytesToKilobitsPerSecond, 2);
            }
        }

        _prevNetInBytes = currentIn;
        _prevNetOutBytes = currentOut;
        _lastNetworkCollectionTime = now;
    }

    /// <summary>
    /// Collects the count of running processes on the system.
    /// </summary>
    private static void CollectProcessCount(MetricsModel metrics)
    {
        metrics.ProcessCount = Process.GetProcesses().Length;
    }

    /// <summary>
    /// Collects TCP connection and listening port statistics.
    /// </summary>
    private void CollectTcpMetrics(MetricsModel metrics)
    {
        var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
        metrics.TcpConnectionsCount = ipProperties.GetActiveTcpConnections().Length;
        metrics.ListeningPortsCount = ipProperties.GetActiveTcpListeners().Length;
    }

    /// <summary>
    /// Collects system uptime in seconds from /proc/uptime.
    /// </summary>
    /// <returns>System uptime in seconds, or null if unable to determine.</returns>
    private static long? CollectUptime()
    {
        try
        {
            string raw = File.ReadAllText(ProcUptimePath).Split(' ')[0];
            return (long)double.Parse(raw, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely executes an asynchronous metric collection operation with error logging.
    /// </summary>
    /// <param name="collector">The async metric collection operation.</param>
    /// <param name="metricName">Display name of the metric being collected (for logging).</param>
    private async Task CollectSafeAsync(Func<Task> collector, string metricName)
    {
        try
        {
            await collector();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting {MetricName}", metricName);
        }
    }

    /// <summary>
    /// Safely executes a synchronous metric collection operation with error logging.
    /// </summary>
    /// <param name="collector">The synchronous metric collection operation.</param>
    /// <param name="metricName">Display name of the metric being collected (for logging).</param>
    private void CollectSafe(Action collector, string metricName)
    {
        try
        {
            collector();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting {MetricName}", metricName);
        }
    }
}
