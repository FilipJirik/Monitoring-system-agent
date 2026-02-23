using System.Text.Json.Serialization;

namespace Monitoring_system_agent.Models;

public class MetricsModel
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("cpuUsagePercent")]
    public double? CpuUsagePercent { get; set; }

    [JsonPropertyName("cpuFreqAvgMhz")]
    public long? CpuFreqAvgMhz { get; set; }

    [JsonPropertyName("cpuTempCelsius")]
    public double? CpuTempCelsius { get; set; }

    [JsonPropertyName("ramUsageMb")]
    public long? RamUsageMb { get; set; }

    [JsonPropertyName("diskUsagePercent")]
    public double? DiskUsagePercent { get; set; }

    [JsonPropertyName("networkInKbps")]
    public double? NetworkInKbps { get; set; }

    [JsonPropertyName("networkOutKbps")]
    public double? NetworkOutKbps { get; set; }

    [JsonPropertyName("uptimeSeconds")]
    public long? UptimeSeconds { get; set; }

    [JsonPropertyName("processCount")]
    public int ProcessCount { get; set; }

    [JsonPropertyName("tcpConnectionsCount")]
    public int TcpConnectionsCount { get; set; }

    [JsonPropertyName("listeningPortsCount")]
    public int ListeningPortsCount { get; set; }
}