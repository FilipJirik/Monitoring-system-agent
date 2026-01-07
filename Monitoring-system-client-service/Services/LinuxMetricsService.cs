using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Monitoring_system_agent.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;

namespace Monitoring_system_agent.Services;

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

    // --- CPU Usage ---
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

    // --- CPU Frequency ---
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
                if (parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double mhz)) // FIXME better solution
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

    // ---  CPU Temperature ---
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

    // --- RAM Usage ---
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

    // --- Disk Usage - using C# DriveInfo instead of /proc/diskstats
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

    // --- Network (Kbps) ---
    private async Task ParseNetwork(MetricsModel metrics)
    {
        if (!File.Exists("/proc/net/dev")) 
            return;

        var lines = await File.ReadAllLinesAsync("/proc/net/dev");
        long currentIn = 0;
        long currentOut = 0;


        foreach (var line in lines.Skip(2)) // skip headers
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

                // Převod: (Bytes * 8 = Bits) / 1000 = Kilobits / seconds = Kbps
                metrics.NetworkInKbps = Math.Round((deltaIn * 8.0) / 1000.0 / seconds, 2); // FIXME: Make into custom method
                metrics.NetworkOutKbps = Math.Round((deltaOut * 8.0) / 1000.0 / seconds, 2);
            }
        }

        _prevNetInBytes = currentIn;
        _prevNetOutBytes = currentOut;
        _lastCollectionTime = now;
    }

    // ---  Uptime ---
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
