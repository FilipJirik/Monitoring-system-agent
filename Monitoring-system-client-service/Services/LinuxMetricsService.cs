using Monitoring_system_agent.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;

namespace Monitoring_system_agent.Services;

public class LinuxMetricsService
{
    private readonly ILogger<LinuxMetricsService> _logger;
    private long _prevCpuTotalTicks;
    private long _prevCpuIdleTicks;
    private long _prevNetInBytes;
    private long _prevNetOutBytes;
    private DateTimeOffset _lastCollectionTime = DateTimeOffset.MinValue;

    public LinuxMetricsService(ILogger<LinuxMetricsService> logger)
    {
        _logger = logger;
    }

    public async Task<MetricsModel> GetMetricsAsync()
    {
        var metrics = new MetricsModel
        {
            Timestamp = DateTimeOffset.UtcNow,
            UptimeSeconds = GetUptime(),
            // Initialize network to 0 to avoid nulls if desired, or keep as null for first run.
            // But user says it is null "all the time", let's ensure we try to get it.
            NetworkInKbps = 0,
            NetworkOutKbps = 0
        };

        try { await ParseCpuUsage(metrics); }
        catch (Exception ex) { _logger.LogError(ex, "Error collecting CPU usage"); }

        try { await ParseCpuFreq(metrics); }
        catch (Exception ex) { _logger.LogError(ex, "Error collecting CPU frequency"); }

        try { await ParseCpuTemp(metrics); }
        catch (Exception ex) { _logger.LogError(ex, "Error collecting CPU temperature"); }

        try { await ParseRam(metrics); }
        catch (Exception ex) { _logger.LogError(ex, "Error collecting RAM usage"); }

        try { ParseDisk(metrics); }
        catch (Exception ex) { _logger.LogError(ex, "Error collecting disk usage"); }

        try { await ParseNetwork(metrics); }
        catch (Exception ex) { _logger.LogError(ex, "Error collecting network stats"); }

        try { CollectProcessAndNetworkMetrics(metrics); }
        catch (Exception ex) { _logger.LogError(ex, "Error collecting process and network metrics"); }

        // Removed ProcessAndServiceCollector calls as requested

        return metrics;
    }

    private async Task ParseCpuUsage(MetricsModel metrics)
    {
        if (!File.Exists("/proc/stat")) return;

        string line = (await File.ReadAllLinesAsync("/proc/stat"))[0];
        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        long idle = long.Parse(parts[4]);
        long iowait = long.Parse(parts[5]);
        long currentIdle = idle + iowait;
        long currentTotal = parts.Skip(1).Take(7).Sum(p => long.TryParse(p, out long v) ? v : 0);

        if (_prevCpuTotalTicks > 0)
        {
            long deltaTotal = currentTotal - _prevCpuTotalTicks;
            long deltaIdle = currentIdle - _prevCpuIdleTicks;

            if (deltaTotal > 0)
                metrics.CpuUsagePercent = Math.Round((1.0 - ((double)deltaIdle / deltaTotal)) * 100.0, 2);
        }

        _prevCpuTotalTicks = currentTotal;
        _prevCpuIdleTicks = currentIdle;
    }

    private async Task ParseCpuFreq(MetricsModel metrics)
    {
        if (!File.Exists("/proc/cpuinfo")) return;

        var lines = await File.ReadAllLinesAsync("/proc/cpuinfo");
        double totalMhz = 0;
        int coreCount = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("cpu MHz") || line.StartsWith("BogoMIPS")) // BogoMIPS as fallback for some ARM
            {
                var parts = line.Split(':');
                if (parts.Length > 1 && double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double mhz))
                {
                    totalMhz += mhz;
                    coreCount++;
                }
            }
        }

        if (coreCount > 0)
            metrics.CpuFreqAvgMhz = (long)(totalMhz / coreCount);
    }

    private async Task ParseCpuTemp(MetricsModel metrics)
    {
        // Try multiple thermal zones
        for (int i = 0; i < 10; i++)
        {
            string path = $"/sys/class/thermal/thermal_zone{i}/temp";
            string typePath = $"/sys/class/thermal/thermal_zone{i}/type";
            
            if (File.Exists(path))
            {
                string type = File.Exists(typePath) ? await File.ReadAllTextAsync(typePath) : "";
                // On many systems, x86_pkg_temp or cpu-thermal are the ones we want
                if (type.Contains("cpu", StringComparison.OrdinalIgnoreCase) || 
                    type.Contains("temp", StringComparison.OrdinalIgnoreCase) || 
                    type.Contains("pkg", StringComparison.OrdinalIgnoreCase))
                {
                    string text = await File.ReadAllTextAsync(path);
                    if (double.TryParse(text.Trim(), out double tempMilli))
                    {
                        metrics.CpuTempCelsius = Math.Round(tempMilli / 1000.0, 1);
                        return; // Found a likely CPU temp
                    }
                }
            }
        }

        // Fallback to thermal_zone0 if nothing else found
        if (metrics.CpuTempCelsius == null && File.Exists("/sys/class/thermal/thermal_zone0/temp"))
        {
            string text = await File.ReadAllTextAsync("/sys/class/thermal/thermal_zone0/temp");
            if (double.TryParse(text.Trim(), out double tempMilli))
                metrics.CpuTempCelsius = Math.Round(tempMilli / 1000.0, 1);
        }
    }

    private async Task ParseRam(MetricsModel metrics)
    {
        if (!File.Exists("/proc/meminfo")) return;

        var lines = await File.ReadAllLinesAsync("/proc/meminfo");
        long totalKb = 0;
        long availableKb = 0;

        foreach (var line in lines)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            if (parts[0] == "MemTotal:") totalKb = long.Parse(parts[1]);
            if (parts[0] == "MemAvailable:") availableKb = long.Parse(parts[1]);
        }

        if (totalKb > 0)
            metrics.RamUsageMb = (totalKb - availableKb) / 1024;
    }

    private void ParseDisk(MetricsModel metrics)
    {
        var drive = DriveInfo.GetDrives().FirstOrDefault(d => (d.Name == "/" || d.Name == "/etc/hosts") && d.IsReady);
        if (drive != null)
        {
            double total = drive.TotalSize;
            double free = drive.AvailableFreeSpace;
            metrics.DiskUsagePercent = Math.Round(((total - free) / total) * 100.0, 2);
        }
    }

    private async Task ParseNetwork(MetricsModel metrics)
    {
        if (!File.Exists("/proc/net/dev"))
            return;

        var lines = await File.ReadAllLinesAsync("/proc/net/dev");
        long currentIn = 0;
        long currentOut = 0;

        foreach (var line in lines.Skip(2))
        {
            var parts = line.Trim().Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 10 || parts[0] == "lo")
                continue;

            // In some cases if split by ':' didn't work as expected because of lack of space
            // the bytes might be in parts[1] or parts[2].
            // Usually it is: [0]: interface, [1]: rx_bytes, ..., [9]: tx_bytes
            if (long.TryParse(parts[1], out long rx)) currentIn += rx;
            if (long.TryParse(parts[9], out long tx)) currentOut += tx;
        }

        var now = DateTimeOffset.UtcNow;

        if (_lastCollectionTime != DateTimeOffset.MinValue)
        {
            double seconds = (now - _lastCollectionTime).TotalSeconds;
            if (seconds > 1) // Ensure at least 1 second passed to avoid spikes
            {
                long deltaIn = Math.Max(0, currentIn - _prevNetInBytes);
                long deltaOut = Math.Max(0, currentOut - _prevNetOutBytes);

                metrics.NetworkInKbps = Math.Round((deltaIn * 8.0) / 1000.0 / seconds, 2);
                metrics.NetworkOutKbps = Math.Round((deltaOut * 8.0) / 1000.0 / seconds, 2);
            }
        }

        _prevNetInBytes = currentIn;
        _prevNetOutBytes = currentOut;
        _lastCollectionTime = now;
    }

    private long? GetUptime()
    {
        try
        {
            string content = File.ReadAllText("/proc/uptime").Split(' ')[0];
            return (long)double.Parse(content, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private void CollectProcessAndNetworkMetrics(MetricsModel metrics)
    {
        // Collect process count
        metrics.ProcessCount = Process.GetProcesses().Length;

        // Collect TCP connections and listening ports using IPGlobalProperties
        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            metrics.TcpConnectionsCount = ipGlobalProperties.GetActiveTcpConnections().Length;
            metrics.ListeningPortsCount = ipGlobalProperties.GetActiveTcpListeners().Length;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting TCP metrics");
        }
    }
}
