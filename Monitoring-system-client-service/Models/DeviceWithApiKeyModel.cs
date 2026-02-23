namespace Monitoring_system_client_service.Models;

public class DeviceWithApiKeyModel
{
    public string Id { get; set; } = default!;
    public string ApiKey { get; set; } = default!;
    public string SetupCommand { get; set; } = default!;
}
