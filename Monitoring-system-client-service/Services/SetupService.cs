using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring_system_client_service.Services
{
    public class SetupService
    {
        private const string _configFile = "agent_config.json";
        private readonly ApiClientService _apiClient;

        public SetupService(ApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task RunSetupAsync(string[] args)
        {
            Console.WriteLine("Running setup...");

            var arguments = ParseArgs(args.Skip(1).ToArray());

            // TODO: save to configuration 
            if (arguments.TryGetValue("interval", out var interval))
            {

            }

            if (arguments.TryGetValue("auth-url", out var authUrl))
            {

            }

            if (arguments.TryGetValue("device-url", out var deviceUrl))
            {

            }
            
            if (arguments.TryGetValue("api-key-url", out var apiKeyUrl))
            {

            }


            if (arguments.TryGetValue("server-url", out var serverUrl) &&
                arguments.TryGetValue("username", out var username) &&
                arguments.TryGetValue("password", out var password) &&
                arguments.TryGetValue("device-name", out var deviceName))
            {
                if (string.IsNullOrEmpty(serverUrl) || 
                    string.IsNullOrEmpty(username) ||
                    string.IsNullOrEmpty(password) ||
                    string.IsNullOrEmpty(deviceName))
                {
                    Console.WriteLine("Unable to find passed arguments");
                    return;
                }

                bool isSuccessful = await TryLoginWithDeviceName(serverUrl, username, password, deviceName);

                if (isSuccessful)
                {
                    Console.WriteLine("Setup complete!");
                    Console.WriteLine("Setup succesfully saved to configuration file");
                    Console.WriteLine("Start the service: sudo systemctl start monitoring-agent");
                }

            }
            else if (arguments.TryGetValue("server-url", out var serverUri) && 
                    arguments.TryGetValue("device-id", out var deviceId) &&
                    arguments.TryGetValue("api-key", out var apiKey))
            {
                if (string.IsNullOrEmpty(serverUrl) ||
                    string.IsNullOrEmpty(deviceId) ||
                    string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("Unable to find passed arguments");
                    return;
                }

                bool isSuccessful = await TryLoginWithApiKey(serverUrl, deviceId, apiKey);

                if (isSuccessful)
                {
                    Console.WriteLine("Setup complete!");
                    Console.WriteLine("Setup succesfully saved to configuration file");
                    Console.WriteLine("Start the service: sudo systemctl start monitoring-agent");
                }

            }
            else
                Console.WriteLine("Invalid arguments for setup command.");
        }

        private async Task<bool> TryLoginWithDeviceName(string serverUrl, string username, string password, string deviceName)
        {
            // POST /api/auth/login
            LoginModel? loginInfo = await _apiClient.LoginAsync(username, password, serverUrl);

            if (loginInfo is null)
            {
                Console.WriteLine("Unable to login using username and password");
                return false;
            }

            // GET /api/devices/regenerate-api-key?name= 
            var device = await _apiClient.GetNewApiKeyByNameAsync(deviceName, loginInfo.Token, serverUrl);

            if (device is null)
            {
                Console.WriteLine($"Unable to get info about device with name: {deviceName}");
                return false;
            }

            ConfigModel config = new ConfigModel()
            {
                BaseUrl = serverUrl,
                DeviceName = deviceName,
                DeviceId = device.Id,
                ApiKey = device.ApiKey,
                Username = username,
                Password = password,
            };


            // POST /api/devices/{deviceId}/metrics - if fails dont save configuration?


            if (!ConfigService.TryToSaveFile(_configFile, config))
            {
                Console.WriteLine("Unable to save configuration to file");
                return false;
            }
            return true;
        }


        private async Task<bool> TryLoginWithApiKey(string serverUrl, string deviceId, string apiKey)
        {
            if (!Guid.TryParse(deviceId, out Guid id))
            {
                Console.WriteLine("Unable to convert Device ID into proper form");
                return false;
            }

            // POST /api/devices/{deviceId}/metrics - if fails dont save configuration?



            ConfigModel config = new ConfigModel()
            {
                BaseUrl = serverUrl,
                DeviceId = id,
                ApiKey = apiKey
            };

            if (!ConfigService.TryToSaveFile(_configFile, config))
            {
                Console.WriteLine("Unable to save configuration to file");
                return false;
            }
            return true;
        }

        public async Task RunCreateDeviceAsync(string[] args)
        {
            Console.WriteLine("Running automatic registration...");

            // TODO
                
        }

        public void PrintConfig()
        {
            Console.WriteLine("Displaying configuration file...");

            string? content = ConfigService.TryReadFile(_configFile);

            if (content is null)
            {
                Console.WriteLine("Error while loading file");
                return;
            }

            Console.WriteLine(content);
        }

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
}

