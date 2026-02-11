using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using System.Runtime.InteropServices;

namespace Monitoring_system_client_service.Services;

public class SetupService
{
    private readonly ApiClientService _apiClient;

    public SetupService(ApiClientService apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task RunSetupAsync(string[] args)
    {
        var argsDict = ParseArgs(args.Skip(1).ToArray());

        if (await TrySetupWithCredentials(argsDict))
            return;

        if (await TrySetupWithApiKey(argsDict))
            return;

        Console.WriteLine("[ERROR] Invalid arguments. Expected one of:");
        Console.WriteLine("  1. --server-url --email --password --device-id [--interval]");
        Console.WriteLine("  2. --server-url --device-id --api-key [--interval]");
    }

    public async Task RunRegisterAsync(string[] args)
    {
        var argsDict = ParseArgs(args.Skip(1).ToArray());

        if (!argsDict.TryGetValue("server-url", out var serverUrl) ||
            !argsDict.TryGetValue("email", out var email) ||
            !argsDict.TryGetValue("password", out var password) ||
            !argsDict.TryGetValue("device-name", out var deviceName) ||
            string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(email) ||
            string.IsNullOrEmpty(password) || string.IsNullOrEmpty(deviceName))
        {
            Console.WriteLine("[ERROR] Missing required arguments for registration");
            return;
        }

        var loginInfo = await _apiClient.LoginAsync(email, password, serverUrl);
        if (loginInfo == null)
        {
            Console.WriteLine("[ERROR] Login failed");
            return;
        }

        string osDescription = RuntimeInformation.OSDescription;
        string ipAddress = SystemInfoService.GetLocalIpAddress();
        string macAddress = SystemInfoService.GetMacAddress();

        var newDevice = await _apiClient.CreateDeviceAsync(
            deviceName, osDescription, ipAddress, macAddress,
            loginInfo.Token, serverUrl);

        if (newDevice == null)
        {
            Console.WriteLine("[ERROR] Failed to create device");
            return;
        }

        var config = new ConfigModel
        {
            BaseUrl = serverUrl,
            DeviceId = newDevice.Id.ToString(),
            ApiKey = newDevice.ApiKey,
        };

        if (argsDict.TryGetValue("interval", out var intervalStr) &&
            int.TryParse(intervalStr, out int interval))
            config.IntervalSeconds = interval;

        SaveConfig(config);
    }

    public void PrintConfig()
    {
        string? content = ConfigService.ReadConfig();
        if (content == null)
        {
            Console.WriteLine("[ERROR] Configuration file not found");
            return;
        }
        Console.WriteLine(content);
    }

    private async Task<bool> TrySetupWithCredentials(Dictionary<string, string?> args)
    {
        if (!args.TryGetValue("server-url", out var serverUrl) ||
            !args.TryGetValue("email", out var email) ||
            !args.TryGetValue("password", out var password) ||
            !args.TryGetValue("device-id", out var deviceId) ||
            string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(email) ||
            string.IsNullOrEmpty(password) || string.IsNullOrEmpty(deviceId))
            return false;

        if (!Guid.TryParse(deviceId, out Guid id))
        {
            Console.WriteLine("[ERROR] Invalid device ID format");
            return false;
        }

        var loginInfo = await _apiClient.LoginAsync(email, password, serverUrl);
        if (loginInfo == null)
        {
            Console.WriteLine("[ERROR] Login failed");
            return false;
        }

        var device = await _apiClient.GetApiKeyByIdAsync(id, loginInfo.Token, serverUrl);
        if (device == null)
        {
            Console.WriteLine("[ERROR] Device not found or failed to regenerate API key");
            return false;
        }

        var config = new ConfigModel
        {
            BaseUrl = serverUrl,
            DeviceId = device.Id.ToString(),
            ApiKey = device.ApiKey,
        };

        if (args.TryGetValue("interval", out var intervalStr) &&
            int.TryParse(intervalStr, out int interval))
            config.IntervalSeconds = interval;

        return SaveConfig(config);
    }

    private async Task<bool> TrySetupWithApiKey(Dictionary<string, string?> args)
    {
        if (!args.TryGetValue("server-url", out var serverUrl) ||
            !args.TryGetValue("device-id", out var deviceId) ||
            !args.TryGetValue("api-key", out var apiKey) ||
            string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(deviceId) ||
            string.IsNullOrEmpty(apiKey))
            return false;

        if (!Guid.TryParse(deviceId, out _))
        {
            Console.WriteLine("[ERROR] Invalid device ID format");
            return false;
        }

        var config = new ConfigModel
        {
            BaseUrl = serverUrl,
            DeviceId = deviceId,
            ApiKey = apiKey,
        };

        if (args.TryGetValue("interval", out var intervalStr) &&
            int.TryParse(intervalStr, out int interval))
            config.IntervalSeconds = interval;

        return SaveConfig(config);
    }

    private bool SaveConfig(ConfigModel model)
    {
        if (ConfigService.SaveConfig(model))
        {
            Console.WriteLine("[INFO] Configuration saved successfully");
            return true;
        }
        Console.WriteLine("[ERROR] Failed to save configuration");
        return false;
    }

    private static Dictionary<string, string?> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var arg in args)
        {
            if (!arg.StartsWith("--"))
                continue;

            var parts = arg.Substring(2).Split('=', 2);
            dict[parts[0]] = parts.Length == 2 ? parts[1] : null;
        }
        return dict;
    }
}


