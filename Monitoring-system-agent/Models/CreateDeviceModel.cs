using System.Text.Json.Serialization;

namespace Monitoring_system_client_service.Models;

public class CreateDeviceModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("operatingSystem")]
    public string OperatingSystem { get; set; } = default!;

    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; set; } = default!;

    [JsonPropertyName("macAddress")]
    public string MacAddress { get; set; } = default!;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("sshEnabled")]
    public bool? SshEnabled { get; set; }
}
