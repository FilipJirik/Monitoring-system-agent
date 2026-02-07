using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Monitoring_system_client_service.Services;

public class SetupService
{
    private readonly ApiClientService _apiClient;
    private readonly IConfiguration _config;
    public SetupService(ApiClientService apiClient, IConfiguration config)
    {
        _apiClient = apiClient;
        _config = config;
    }

    /// <summary>
    /// Runs the setup process by parsing command-line arguments and configuring the agent.
    /// </summary>
    /// <param name="args">Command-line arguments containing server URL, credentials, and device information</param>
    public async Task RunSetupAsync(string[] args)
    {
        Console.WriteLine("Running setup...");

        var argsDict = ParseArgs(args.Skip(1).ToArray());

        int? interval = null;

        if (argsDict.TryGetValue("interval", out var intervalStr) 
            && int.TryParse(intervalStr, out int i))
            interval = i;

        if (argsDict.TryGetValue("server-url", out var serverUrl) &&
            argsDict.TryGetValue("email", out var email) &&
            argsDict.TryGetValue("password", out var password) &&
            argsDict.TryGetValue("device-id", out var deviceId))
        {
            if (string.IsNullOrEmpty(serverUrl) ||
                string.IsNullOrEmpty(email) ||
                string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(deviceId))
            {
                Console.WriteLine("Unable to find passed arguments");
                return;
            }

            await TryLoginWithDeviceId(serverUrl, email, password, deviceId, interval);
        }
        else if (argsDict.TryGetValue("server-url", out var serverUri) && 
                argsDict.TryGetValue("device-id", out var deviceIdArg) &&
                argsDict.TryGetValue("api-key", out var apiKey))
        {
            if (string.IsNullOrEmpty(serverUri) ||
                string.IsNullOrEmpty(deviceIdArg) ||
                string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Unable to find passed arguments");
                return;
            }

            await TryLoginWithApiKey(serverUri, deviceIdArg, apiKey, interval);
        }
        else
        {
            Console.WriteLine("Invalid arguments. Please provide server-url and credentials.");
        }
    }

    /// <summary>
    /// Runs the automatic device registration process by creating a new device on the server.
    /// </summary>
    /// <param name="args">Command-line arguments containing server URL, email, password, and device name</param>
    public async Task RunCreateDeviceAsync(string[] args)
    {
        Console.WriteLine("Running automatic registration...");

        var argsDict = ParseArgs(args.Skip(1).ToArray());

        int? interval = null;

        if (argsDict.TryGetValue("interval", out var intervalStr)
            && int.TryParse(intervalStr, out int i))
            interval = i;

        if (argsDict.TryGetValue("server-url", out var serverUrl) &&
            argsDict.TryGetValue("email", out var email) &&
            argsDict.TryGetValue("password", out var password) &&
            argsDict.TryGetValue("device-name", out var deviceName))
        {
            if (string.IsNullOrEmpty(serverUrl) ||
                string.IsNullOrEmpty(email) ||
                string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(deviceName))
            {
                Console.WriteLine("Unable to find passed arguments");
                return;
            }

            var loginInfo = await _apiClient.LoginAndGetTokenAsync(email!, password!, serverUrl!);

            if (loginInfo == null)
            {
                Console.WriteLine("Unable to login using email and password");
                return;
            }

            string hostName = System.Net.Dns.GetHostName();
            string osDescription = RuntimeInformation.OSDescription; 
            string ipAddress = DeviceRegistrationService.GetLocalIpAddress();
            string macAddress = DeviceRegistrationService.GetMacAddress();


            var newDevice = await _apiClient.CreateDeviceAsync(
                deviceName!,
                osDescription,
                ipAddress,
                macAddress,
                loginInfo.Token,
                serverUrl!
            );

            if (newDevice is not null)
            {
                var config = new ConfigModel
                {
                    BaseUrl = serverUrl!,
                    DeviceId = newDevice.Id.ToString(),
                    ApiKey = newDevice.ApiKey,
                };
                if (interval is not null)
                    config.IntervalSeconds = interval.Value;

                if (!SaveAndPrintMessage(config))
                    return;
            }
            else
            {
                Console.WriteLine("Failed to create device.");
            }
            return;
        }

        Console.WriteLine("Missing arguments for registration.");
    }

    /// <summary>
    /// Configures the agent using email, password, and device ID by regenerating the API key.
    /// </summary>
    /// <param name="serverUrl">The base URL of the API server</param>
    /// <param name="email">The user's email address</param>
    /// <param name="password">The user's password</param>
    /// <param name="deviceId">The unique identifier of the device</param>
    /// <param name="interval">Optional interval in seconds for metrics collection</param>
    /// <returns>True if the configuration was saved successfully, false otherwise</returns>
    private async Task<bool> TryLoginWithDeviceId(string serverUrl, string email, string password, string deviceId, int? interval)
    {
        if (!Guid.TryParse(deviceId, out Guid id))
        {
            Console.WriteLine("Invalid device ID format");
            return false;
        }

        var loginInfo = await _apiClient.LoginAndGetTokenAsync(email, password, serverUrl); 

        if (loginInfo is null)
        {
            Console.WriteLine("Unable to login using email and password");
            return false;
        }

        var device = await _apiClient.GetNewApiKeyByIdAsync(id, loginInfo.Token, serverUrl);

        if (device is null)
        {
            Console.WriteLine($"Device with ID '{deviceId}' not found or failed to regenerate API key.");
            return false;
        }

        var config = new ConfigModel()
        {
            BaseUrl = serverUrl,
            DeviceId = device.Id.ToString(),
            ApiKey = device.ApiKey,
        };
        if (interval is not null) 
            config.IntervalSeconds = interval.Value;

        return SaveAndPrintMessage(config);
    }

    /// <summary>
    /// Configures the agent using an existing device ID and API key without requiring login.
    /// </summary>
    /// <param name="serverUrl">The base URL of the API server</param>
    /// <param name="deviceId">The unique identifier of the device</param>
    /// <param name="apiKey">The API key for the device</param>
    /// <param name="interval">Optional interval in seconds for metrics collection</param>
    /// <returns>True if the configuration was saved successfully, false otherwise</returns>
    private async Task<bool> TryLoginWithApiKey(string serverUrl, string deviceId, string apiKey, int? interval)
    {
        if (!Guid.TryParse(deviceId, out Guid id))
        {
            Console.WriteLine("Unable to convert Device ID into proper form");
            return false;
        }

        var config = new ConfigModel()
        {
            BaseUrl = serverUrl,
            DeviceId = deviceId,
            ApiKey = apiKey,
        };
        if (interval is not null)
            config.IntervalSeconds = interval.Value;

        return SaveAndPrintMessage(config);
    }
    /// <summary>
    /// Displays the contents of the configuration file to the console.
    /// </summary>
    public void PrintConfig()
    {
        Console.WriteLine("Displaying configuration file...");

        string? content = ConfigService.TryReadFile();

        if (content is null)
        {
            Console.WriteLine("Error while loading file");
            return;
        }

        Console.WriteLine(content);
    }

    /// <summary>
    /// Saves the configuration model to a file and displays success messages.
    /// </summary>
    /// <param name="model">The configuration model to save</param>
    /// <returns>True if the configuration was saved successfully, false otherwise</returns>
    private bool SaveAndPrintMessage(ConfigModel model)
    {
        if (ConfigService.TrySaveFile(model))
        {
            Console.WriteLine("Setup complete...");
            Console.WriteLine("Configuration saved");
            Console.WriteLine("Start the service: sudo systemctl start monitoring-agent");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Parses command-line arguments into a dictionary of key-value pairs.
    /// </summary>
    /// <param name="args">Array of command-line arguments in the format --key=value</param>
    /// <returns>A dictionary containing parsed arguments</returns>
    private static Dictionary<string, string?> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string?>();

        foreach (var arg in args)
        {
            if (!arg.StartsWith("--"))
                continue;

            var parts = arg.Substring(2).Split('=', 2);

            string key = parts[0];
            string? value = parts.Length == 2 ? parts[1] : null;

            dict[key] = value;
        }
        return dict;
    }
}


