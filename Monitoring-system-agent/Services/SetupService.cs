using Monitoring_system_client_service.Configuration;
using Monitoring_system_client_service.Models;
using System.Runtime.InteropServices;

namespace Monitoring_system_client_service.Services;

/// <summary>
/// Provides device setup and registration functionality for the monitoring client.
/// Handles interactive configuration, device registration, and API key management.
/// </summary>
public class SetupService
{
    private readonly ApiClientService _apiClient;

    // Configuration interval defaults
    private const int MinimumIntervalSeconds = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetupService"/> class.
    /// </summary>
    /// <param name="apiClient">The API client service for backend communication.</param>
    public SetupService(ApiClientService apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Executes the setup command to configure the client using credentials or API key.
    /// Supports two setup flows:
    /// 1. Using email/password credentials with an existing device ID
    /// 2. Using a pre-generated API key
    /// </summary>
    /// <param name="args">Dictionary of command-line arguments.</param>
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

    /// <summary>
    /// Executes the register command to create a new device on the backend.
    /// Automatically detects hardware information and registers the device.
    /// </summary>
    /// <param name="args">Dictionary of command-line arguments.</param>
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

        // Authenticate user
        var loginInfo = await _apiClient.LoginAsync(email, password, serverUrl);
        if (loginInfo == null)
        {
            Console.WriteLine("[ERROR] Login failed. Check your credentials and server URL.");
            return;
        }

        // Build device model with system information
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

        // Create device on backend
        var deviceWithApiKey = await _apiClient.CreateDeviceAsync(deviceModel, loginInfo.Token, serverUrl);
        if (deviceWithApiKey == null)
        {
            Console.WriteLine("[ERROR] Failed to create device");
            return;
        }

        // Save configuration
        var config = new ConfigModel
        {
            BaseUrl = serverUrl,
            DeviceId = deviceWithApiKey.Id.ToString(),
            ApiKey = deviceWithApiKey.ApiKey,
        };

        ApplyOptionalInterval(args, config);
        ApplyOptionalSelfSignedCertificates(args, config);
        SaveAndReport(config);
    }

    /// <summary>
    /// Displays the current configuration file contents.
    /// </summary>
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

    /// <summary>
    /// Attempts to set up the client using email and password credentials with an existing device ID.
    /// </summary>
    /// <param name="args">Dictionary of command-line arguments.</param>
    /// <returns>True if setup was successful; false otherwise.</returns>
    private async Task<bool> TrySetupWithCredentials(Dictionary<string, string?> args)
    {
        if (!TryGetRequired(args, out var serverUrl, "server-url") ||
            !TryGetRequired(args, out var email, "email") ||
            !TryGetRequired(args, out var password, "password") ||
            !TryGetRequired(args, out var deviceId, "device-id"))
            return false;

        if (!Guid.TryParse(deviceId, out var parsedDeviceId))
        {
            Console.WriteLine("[ERROR] Invalid device ID format. Expected a valid GUID.");
            return false;
        }

        // Authenticate user
        var loginInfo = await _apiClient.LoginAsync(email, password, serverUrl);
        if (loginInfo == null)
        {
            Console.WriteLine("[ERROR] Login failed. Check your credentials and server URL.");
            return false;
        }

        // Retrieve API key for the device
        var deviceWithApiKey = await _apiClient.GetApiKeyByIdAsync(parsedDeviceId, loginInfo.Token, serverUrl);
        if (deviceWithApiKey == null)
        {
            Console.WriteLine("[ERROR] Device not found or failed to regenerate API key");
            return false;
        }

        // Save configuration
        var config = new ConfigModel
        {
            BaseUrl = serverUrl,
            DeviceId = deviceWithApiKey.Id.ToString(),
            ApiKey = deviceWithApiKey.ApiKey,
        };

        ApplyOptionalInterval(args, config);
        ApplyOptionalSelfSignedCertificates(args, config);
        return SaveAndReport(config);
    }

    /// <summary>
    /// Attempts to set up the client using a pre-generated API key.
    /// </summary>
    /// <param name="args">Dictionary of command-line arguments.</param>
    /// <returns>True if setup was successful; false otherwise.</returns>
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

        // Create configuration directly from API key
        var config = new ConfigModel
        {
            BaseUrl = serverUrl,
            DeviceId = deviceId,
            ApiKey = apiKey,
        };

        ApplyOptionalInterval(args, config);
        ApplyOptionalSelfSignedCertificates(args, config);
        return SaveAndReport(config);
    }

    /// <summary>
    /// Attempts to extract a required argument from the arguments dictionary.
    /// </summary>
    /// <param name="args">Dictionary of command-line arguments.</param>
    /// <param name="value">Output: the argument value if found.</param>
    /// <param name="key">The key name to look for.</param>
    /// <returns>True if the argument exists and is not empty; false otherwise.</returns>
    private static bool TryGetRequired(Dictionary<string, string?> args, out string value, string key)
    {
        if (args.TryGetValue(key, out var retrievedValue) && !string.IsNullOrEmpty(retrievedValue))
        {
            value = retrievedValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Applies the optional --interval argument to the configuration if provided and valid.
    /// </summary>
    /// <param name="args">Dictionary of command-line arguments.</param>
    /// <param name="config">The configuration model to update.</param>
    private static void ApplyOptionalInterval(Dictionary<string, string?> args, ConfigModel config)
    {
        if (args.TryGetValue("interval", out var intervalStr) &&
            int.TryParse(intervalStr, out int interval) &&
            interval >= MinimumIntervalSeconds)
        {
            config.IntervalSeconds = interval;
        }
    }
    
    /// <summary>
    /// Applies the optional --allow-self-signed-certificates argument to the configuration if provided.
    /// </summary>
    /// <param name="args">Dictionary of command-line arguments.</param>
    /// <param name="config">The configuration model to update.</param>
    private static void ApplyOptionalSelfSignedCertificates(Dictionary<string, string?> args, ConfigModel config)
    {
        if (args.TryGetValue("allow-self-signed-certificates", out var certStr) &&
            bool.TryParse(certStr, out bool allow))
        {
            config.AllowSelfSignedCertificates = allow;
        }
    }

    /// <summary>
    /// Saves the configuration to disk and reports the result to the console.
    /// </summary>
    /// <param name="config">The configuration model to save.</param>
    /// <returns>True if the configuration was saved successfully; false otherwise.</returns>
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
