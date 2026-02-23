using Monitoring_system_client_service.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;

namespace Monitoring_system_client_service.Services;

public class LinuxMetricsService
{
    private readonly ILogger<LinuxMetricsService> _logger;

    // Previous-sample state for delta calculations
    private long _prevCpuTotalTicks;
    private long _prevCpuIdleTicks;
    private long _prevNetInBytes;
    private long _prevNetOutBytes;
    private DateTimeOffset _lastNetworkCollectionTime = DateTimeOffset.MinValue;

    // Proc filesystem paths
    private const string ProcStat = "/proc/stat";
    private const string ProcCpuInfo = "/proc/cpuinfo";
    private const string ProcMemInfo = "/proc/meminfo";
    private const string ProcUptime = "/proc/uptime";
    private const string ProcNetDev = "/proc/net/dev";
    private const string ThermalZoneBasePath = "/sys/class/thermal/thermal_zone";

    // Format constants
    private const int MaxThermalZones = 10;
    private const int ProcNetDevHeaderLines = 2;
    private const int ProcNetDevRxBytesIndex = 1;
    private const int ProcNetDevTxBytesIndex = 9;
    private const int ProcNetDevMinColumns = 10;
    private const string LoopbackInterface = "lo";
    private const double MillidegreesToCelsius = 1000.0;
    private const double BitsPerByte = 8.0;
    private const double KiloBitsPerBit = 1000.0;
    private const double MinNetworkIntervalSeconds = 1.0;

    public LinuxMetricsService(ILogger<LinuxMetricsService> logger)
    {
        _logger = logger;
    }

    public async Task<MetricsModel> GetMetricsAsync()
    {
        var metrics = new MetricsModel
        {
            Timestamp = DateTimeOffset.UtcNow,
            UptimeSeconds = CollectUptime(),
            NetworkInKbps = 0,
            NetworkOutKbps = 0
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

    private async Task CollectCpuUsageAsync(MetricsModel metrics)
    {
        if (!File.Exists(ProcStat)) return;

        string firstLine = (await File.ReadAllLinesAsync(ProcStat))[0];
        var columns = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // /proc/stat format: cpu user nice system idle iowait irq softirq
        long idle = long.Parse(columns[4]);
        long iowait = long.Parse(columns[5]);
        long currentIdle = idle + iowait;
        long currentTotal = columns.Skip(1).Take(7).Sum(c => long.TryParse(c, out long v) ? v : 0);

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

    private async Task CollectCpuFrequencyAsync(MetricsModel metrics)
    {
        if (!File.Exists(ProcCpuInfo)) return;

        var lines = await File.ReadAllLinesAsync(ProcCpuInfo);
        double totalMhz = 0;
        int coreCount = 0;

        foreach (var line in lines)
        {
            // "cpu MHz" on x86, "BogoMIPS" as ARM fallback
            if (!line.StartsWith("cpu MHz") && !line.StartsWith("BogoMIPS"))
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

    private async Task CollectCpuTemperatureAsync(MetricsModel metrics)
    {
        // Search thermal zones for a CPU-related sensor
        for (int i = 0; i < MaxThermalZones; i++)
        {
            string tempPath = $"{ThermalZoneBasePath}{i}/temp";
            string typePath = $"{ThermalZoneBasePath}{i}/type";

            if (!File.Exists(tempPath))
                continue;

            string zoneType = File.Exists(typePath) ? await File.ReadAllTextAsync(typePath) : "";

            bool isCpuSensor = zoneType.Contains("cpu", StringComparison.OrdinalIgnoreCase)
                            || zoneType.Contains("pkg", StringComparison.OrdinalIgnoreCase)
                            || zoneType.Contains("temp", StringComparison.OrdinalIgnoreCase);

            if (!isCpuSensor)
                continue;

            if (TryReadTemperature(await File.ReadAllTextAsync(tempPath), out double celsius))
            {
                metrics.CpuTempCelsius = celsius;
                return;
            }
        }

        // Fallback: thermal_zone0
        string fallbackPath = $"{ThermalZoneBasePath}0/temp";
        if (metrics.CpuTempCelsius == null && File.Exists(fallbackPath))
        {
            if (TryReadTemperature(await File.ReadAllTextAsync(fallbackPath), out double celsius))
                metrics.CpuTempCelsius = celsius;
        }
    }

    private static bool TryReadTemperature(string raw, out double celsius)
    {
        celsius = 0;
        if (!double.TryParse(raw.Trim(), out double millidegrees))
            return false;

        celsius = Math.Round(millidegrees / MillidegreesToCelsius, 1);
        return true;
    }
    private async Task CollectMemoryUsageAsync(MetricsModel metrics)
    {
        if (!File.Exists(ProcMemInfo)) return;

        var lines = await File.ReadAllLinesAsync(ProcMemInfo);
        long totalKb = 0;
        long availableKb = 0;

        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            switch (parts[0])
            {
                case "MemTotal:": totalKb = long.Parse(parts[1]); break;
                case "MemAvailable:": availableKb = long.Parse(parts[1]); break;
            }
        }

        if (totalKb > 0)
            metrics.RamUsageMb = (totalKb - availableKb) / 1024;
    }
    private static void CollectDiskUsage(MetricsModel metrics)
    {
        var rootDrive = DriveInfo.GetDrives()
            .FirstOrDefault(d => d.Name == "/" && d.IsReady);

        if (rootDrive == null) return;

        double total = rootDrive.TotalSize;
        double free = rootDrive.AvailableFreeSpace;
        metrics.DiskUsagePercent = Math.Round((total - free) / total * 100.0, 2);
    }
    private async Task CollectNetworkThroughputAsync(MetricsModel metrics)
    {
        if (!File.Exists(ProcNetDev)) return;

        var lines = await File.ReadAllLinesAsync(ProcNetDev);
        long currentIn = 0;
        long currentOut = 0;

        // Skip header lines
        foreach (var line in lines.Skip(ProcNetDevHeaderLines))
        {
            var columns = line.Trim().Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (columns.Length < ProcNetDevMinColumns || columns[0] == LoopbackInterface)
                continue;

            // columns: [interface, rx_bytes, rx_packets, ..., tx_bytes, ...]
            if (long.TryParse(columns[ProcNetDevRxBytesIndex], out long rx)) currentIn += rx;
            if (long.TryParse(columns[ProcNetDevTxBytesIndex], out long tx)) currentOut += tx;
        }

        var now = DateTimeOffset.UtcNow;

        if (_lastNetworkCollectionTime != DateTimeOffset.MinValue)
        {
            double elapsedSeconds = (now - _lastNetworkCollectionTime).TotalSeconds;

            if (elapsedSeconds > MinNetworkIntervalSeconds)
            {
                long deltaIn = Math.Max(0, currentIn - _prevNetInBytes);
                long deltaOut = Math.Max(0, currentOut - _prevNetOutBytes);

                metrics.NetworkInKbps = Math.Round(deltaIn * BitsPerByte / KiloBitsPerBit / elapsedSeconds, 2);
                metrics.NetworkOutKbps = Math.Round(deltaOut * BitsPerByte / KiloBitsPerBit / elapsedSeconds, 2);
            }
        }

        _prevNetInBytes = currentIn;
        _prevNetOutBytes = currentOut;
        _lastNetworkCollectionTime = now;
    }

    private static void CollectProcessCount(MetricsModel metrics)
    {
        metrics.ProcessCount = Process.GetProcesses().Length;
    }

    private void CollectTcpMetrics(MetricsModel metrics)
    {
        var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
        metrics.TcpConnectionsCount = ipProperties.GetActiveTcpConnections().Length;
        metrics.ListeningPortsCount = ipProperties.GetActiveTcpListeners().Length;
    }

    private static long? CollectUptime()
    {
        try
        {
            string raw = File.ReadAllText(ProcUptime).Split(' ')[0];
            return (long)double.Parse(raw, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

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
