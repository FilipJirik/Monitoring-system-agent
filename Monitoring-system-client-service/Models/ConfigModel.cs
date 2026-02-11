namespace Monitoring_system_agent.Models
{
    public class ConfigModel
    {
        public string BaseUrl { get; set; } = "https://localhost:8080";
        public string DeviceId { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public int IntervalSeconds { get; set; } = 10;
    }
}
