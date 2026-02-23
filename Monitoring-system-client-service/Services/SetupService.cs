using Monitoring_system_client_service.Configuration;
using Monitoring_system_client_service.Models;
using System.Runtime.InteropServices;

namespace Monitoring_system_client_service.Services;

public class SetupService
{
    private readonly ApiClientService _apiClient;

    public SetupService(ApiClientService apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task RunSetupAsync(Dictionary<string, string?> args)
    {
        if (await TrySetupWithCredentials(args))
            return;

        if (TrySetupWithApiKey(args))
            return;

        Console.WriteLine("[ERROR] Invalid arguments. Expected one of:");
        Console.WriteLine("  1. --server-url --email --password --device-id [--interval]");
        Console.WriteLine("  2. --server-url --device-id --api-key [--interval]");
    }

    public async Task RunRegisterAsync(Dictionary<string, string?> args)
    {
        if (!TryGetRequired(args, out var serverUrl, "server-url") ||
            !TryGetRequired(args, out var email, "email") ||
            !TryGetRequired(args, out var password, "password") ||
            !TryGetRequired(args, out var deviceName, "device-name"))
        {
            Console.WriteLine("[ERROR] Missing required arguments for registration");
            Console.WriteLine("  Required: --server-url --email --password --device-name");
            return;
        }

        // POST /api/auth
        var loginInfo = await _apiClient.LoginAsync(email, password, serverUrl);
        if (loginInfo == null)
        {
            Console.WriteLine("[ERROR] Login failed. Check your credentials and server URL.");
            return;
        }

        var deviceModel = new CreateDeviceModel
        {
            Name = deviceName,
            OperatingSystem = RuntimeInformation.OSDescription,
            IpAddress = SystemInfoService.GetLocalIpAddress(),
            MacAddress = SystemInfoService.GetMacAddress(),
            Description = SystemInfoService.GetDeviceDescription(),
            Model = SystemInfoService.GetHardwareModel(),
            SshEnabled = SystemInfoService.IsSshEnabled(),
        };

        // POST /api/devices
        var result = await _apiClient.CreateDeviceAsync(deviceModel, loginInfo.Token, serverUrl);
        if (result == null)
        {
            Console.WriteLine("[ERROR] Failed to create device");
            return;
        }

        var config = new ConfigModel
        {
            BaseUrl = serverUrl,
            DeviceId = result.Id.ToString(),
            ApiKey = result.ApiKey,
        };

        ApplyOptionalInterval(args, config);
        SaveAndReport(config);
    }

    public void PrintConfig()
    {
        string? content = ConfigService.ReadConfig();
        if (content == null)
        {
            Console.WriteLine($"[ERROR] Configuration file '{ConfigService.FileName}' not found");
            return;
        }
        Console.WriteLine(content);
    }

    private async Task<bool> TrySetupWithCredentials(Dictionary<string, string?> args)
    {
        if (!TryGetRequired(args, out var serverUrl, "server-url") ||
            !TryGetRequired(args, out var email, "email") ||
            !TryGetRequired(args, out var password, "password") ||
            !TryGetRequired(args, out var deviceId, "device-id"))
            return false;

        if (!Guid.TryParse(deviceId, out Guid id))
        {
            Console.WriteLine("[ERROR] Invalid device ID format. Expected a valid GUID.");
            return false;
        }

        // POST /api/auth
        var loginInfo = await _apiClient.LoginAsync(email, password, serverUrl);
        if (loginInfo == null)
        {
            Console.WriteLine("[ERROR] Login failed. Check your credentials and server URL.");
            return false;
        }

        // POST /api/devices/{id}
        var deviceWithApiKey = await _apiClient.GetApiKeyByIdAsync(id, loginInfo.Token, serverUrl);
        if (deviceWithApiKey == null)
        {
            Console.WriteLine("[ERROR] Device not found or failed to regenerate API key");
            return false;
        }

        var config = new ConfigModel
        {
            BaseUrl = serverUrl,
            DeviceId = deviceWithApiKey.Id.ToString(),
            ApiKey = deviceWithApiKey.ApiKey,
        };

        ApplyOptionalInterval(args, config);
        return SaveAndReport(config);
    }

    private bool TrySetupWithApiKey(Dictionary<string, string?> args)
    {
        if (!TryGetRequired(args, out var serverUrl, "server-url") ||
            !TryGetRequired(args, out var deviceId, "device-id") ||
            !TryGetRequired(args, out var apiKey, "api-key"))
            return false;

        if (!Guid.TryParse(deviceId, out _))
        {
            Console.WriteLine("[ERROR] Invalid device ID format. Expected a valid GUID.");
            return false;
        }

        var config = new ConfigModel
        {
            BaseUrl = serverUrl,
            DeviceId = deviceId,
            ApiKey = apiKey,
        };

        ApplyOptionalInterval(args, config);
        return SaveAndReport(config);
    }

    /// <summary>
    /// Tries to extract a required argument. Returns false if missing or empty.
    /// </summary>
    private static bool TryGetRequired(Dictionary<string, string?> args, out string value, string key)
    {
        if (args.TryGetValue(key, out var raw) && !string.IsNullOrEmpty(raw))
        {
            value = raw;
            return true;
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Applies the optional --interval argument to the config if present and valid.
    /// </summary>
    private static void ApplyOptionalInterval(Dictionary<string, string?> args, ConfigModel config)
    {
        if (args.TryGetValue("interval", out var intervalStr) &&
            int.TryParse(intervalStr, out int interval) && interval > 0)
        {
            config.IntervalSeconds = interval;
        }
    }

    private static bool SaveAndReport(ConfigModel config)
    {
        if (ConfigService.SaveConfig(config))
        {
            Console.WriteLine("[INFO] Configuration saved successfully");
            return true;
        }

        Console.WriteLine("[ERROR] Failed to save configuration");
        return false;
    }
}
