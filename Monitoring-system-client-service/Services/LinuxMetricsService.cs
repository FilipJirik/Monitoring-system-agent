using System;
using System.Linq;
using System.Threading.Tasks;
using Monitoring_system_agent.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Monitoring_system_agent.Services;

/// <summary>
/// Service for collecting Linux system metrics including CPU, memory, disk, and network statistics.
/// </summary>
public class LinuxMetricsService
{
    private readonly ILogger<LinuxMetricsService> _logger;
    private long _prevCpuTotalTicks = 0;
    private long _prevCpuIdleTicks = 0;

    private long _prevNetInBytes = 0;
    private long _prevNetOutBytes = 0;
    private DateTimeOffset _lastCollectionTime = DateTimeOffset.MinValue;

    public LinuxMetricsService(ILogger<LinuxMetricsService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Collects all system metrics and returns them as a MetricsModel.
    /// </summary>
    /// <returns>A MetricsModel containing all collected system metrics</returns>
    public async Task<MetricsModel> GetMetricsAsync()
    {
        var metrics = new MetricsModel
        {
            Timestamp = DateTimeOffset.UtcNow,
            UptimeSeconds = GetUptime()
        };

        try { 
            await ParseCpuUsage(metrics); 
        } 
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "Error CPU Usage"); 
        }
        try 
        { 
            await ParseCpuFreq(metrics); 
        } 
        catch (Exception ex) 
        {
            _logger.LogError(ex, "Error CPU Freq"); 
        }
        try 
        {
            await ParseCpuTemp(metrics); 
        } 
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "Error CPU Temp"); 
        }
        try 
        { 
            await ParseRam(metrics); 
        } 
        catch (Exception ex) 
        {
            _logger.LogError(ex, "Error RAM"); 
        }
        try 
        { 
            ParseDisk(metrics); 
        } 
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "Error Disk"); 
        }
        try 
        { 
            await ParseNetwork(metrics); 
        } 
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Network"); 
        }

        return metrics;
    }

    /// <summary>
    /// Parses CPU usage percentage from /proc/stat.
    /// </summary>
    /// <param name="metrics">The metrics model to update with CPU usage data</param>
    private async Task ParseCpuUsage(MetricsModel metrics)
    {
        if (!File.Exists("/proc/stat")) return;
        string line = (await File.ReadAllLinesAsync("/proc/stat"))[0];
        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        long idle = long.Parse(parts[4]);
        long iowait = long.Parse(parts[5]);
        long currentIdle = idle + iowait;

        long currentTotal = 0;
        
        for (int i = 1; i < parts.Length && i < 8; i++)
            if (long.TryParse(parts[i], out long val)) currentTotal += val;

        if (_prevCpuTotalTicks > 0)
        {
            long deltaTotal = currentTotal - _prevCpuTotalTicks;
            long deltaIdle = currentIdle - _prevCpuIdleTicks;

            if (deltaTotal > 0)
            {
                double usage = (1.0 - ((double)deltaIdle / deltaTotal)) * 100.0;
                metrics.CpuUsagePercent = Math.Round(usage, 2);
            }
        }

        _prevCpuTotalTicks = currentTotal;
        _prevCpuIdleTicks = currentIdle;
    }

    /// <summary>
    /// Parses average CPU frequency from /proc/cpuinfo.
    /// </summary>
    /// <param name="metrics">The metrics model to update with CPU frequency data</param>
    private async Task ParseCpuFreq(MetricsModel metrics)
    {
        if (!File.Exists("/proc/cpuinfo")) return;

        var lines = await File.ReadAllLinesAsync("/proc/cpuinfo");
        double totalMhz = 0;
        int coreCount = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("cpu MHz"))
            {
                var parts = line.Split(':');
                if (parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double mhz))
                {
                    totalMhz += mhz;
                    coreCount++;
                }
            }
        }

        if (coreCount > 0)
        {
            metrics.CpuFreqAvgMhz = (long)(totalMhz / coreCount);
        }
    }

    /// <summary>
    /// Parses CPU temperature from the thermal zone file.
    /// </summary>
    /// <param name="metrics">The metrics model to update with CPU temperature data</param>
    private async Task ParseCpuTemp(MetricsModel metrics)
    {
        string path = "/sys/class/thermal/thermal_zone0/temp"; // most common, but not all use it
        if (File.Exists(path))
        {
            string text = await File.ReadAllTextAsync(path);
            if (double.TryParse(text.Trim(), out double tempMilli))
            {
                metrics.CpuTempCelsius = Math.Round(tempMilli / 1000.0, 1);
            }
        }
    }

    /// <summary>
    /// Parses RAM usage from /proc/meminfo.
    /// </summary>
    /// <param name="metrics">The metrics model to update with RAM usage data</param>
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
        {
            long usedKb = totalKb - availableKb;
            metrics.RamUsageMb = usedKb / 1024;
        }
    }

    /// <summary>
    /// Parses disk usage for the root filesystem using DriveInfo.
    /// </summary>
    /// <param name="metrics">The metrics model to update with disk usage data</param>
    private void ParseDisk(MetricsModel metrics)
    {
        var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.Name == "/" && d.IsReady);
        if (drive != null)
        {
            double total = drive.TotalSize;
            double free = drive.AvailableFreeSpace;
            metrics.DiskUsagePercent = Math.Round(((total - free) / total) * 100.0, 2);
        }
    }

    /// <summary>
    /// Parses network statistics from /proc/net/dev and calculates network throughput in Kbps.
    /// </summary>
    /// <param name="metrics">The metrics model to update with network statistics</param>
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
            if (parts.Length < 10) 
                continue;

            // loopback
            if (parts[0] == "lo")
                continue;

            // parts[0] = interface name, parts[1] = RX bytes, parts[9] = TX bytes
            if (long.TryParse(parts[1], out long rx)) currentIn += rx;
            if (long.TryParse(parts[9], out long tx)) currentOut += tx;
        }

        var now = DateTimeOffset.UtcNow;

        // if not first run - need starting value for comparing
        if (_lastCollectionTime != DateTimeOffset.MinValue)
        {
            double seconds = (now - _lastCollectionTime).TotalSeconds;
            if (seconds > 0)
            {
                long deltaIn = currentIn - _prevNetInBytes;
                long deltaOut = currentOut - _prevNetOutBytes;

                if (deltaIn < 0) 
                    deltaIn = 0;

                if (deltaOut < 0) 
                    deltaOut = 0;

                metrics.NetworkInKbps = Math.Round((deltaIn * 8.0) / 1000.0 / seconds, 2);
                metrics.NetworkOutKbps = Math.Round((deltaOut * 8.0) / 1000.0 / seconds, 2);
            }
        }

        _prevNetInBytes = currentIn;
        _prevNetOutBytes = currentOut;
        _lastCollectionTime = now;
    }

    /// <summary>
    /// Retrieves the system uptime in seconds from /proc/uptime.
    /// </summary>
    /// <returns>The uptime in seconds, or null if unable to determine</returns>
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
}
