using Microsoft.Extensions.Configuration;

namespace Monitoring_system_client_service.Models;

public class ConfigModel
{
    [ConfigurationKeyName("base_url")]
    public string BaseUrl { get; set; } = "https://localhost:8080";

    [ConfigurationKeyName("device_id")]
    public string DeviceId { get; set; } = "";

    [ConfigurationKeyName("api_key")]
    public string ApiKey { get; set; } = "";

    [ConfigurationKeyName("interval_seconds")]
    public int IntervalSeconds { get; set; } = 10;

    [ConfigurationKeyName("allow_self_signed_certificates")]
    public bool AllowSelfSignedCertificates { get; set; } = true;
}
