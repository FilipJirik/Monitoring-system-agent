using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using System.Runtime.InteropServices;

namespace Monitoring_system_agent
{
    internal class Program
    {
        private const string _configFile = "";
        private const string _credentialsFile = "";

        private readonly ConfigModel? _configModel = null;
        private readonly ApiClientService _apiClientService;

        public Program(ApiClientService apiClientService)
        {
            _apiClientService = apiClientService;
        }

        public static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "login")
            {
                await TryCreateConfigFromArgsAsync(args);
                return;
            }
            else if (args.Length > 0 && args[0] == "register")
            {
                await TryRegisterDeviceFromArgsAsync(args);
                return;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine("This tool only works on Linux devices (for now)"); 
                return;
            }

            if (!ConfigService.TryLoadConfig(_configFile, out ConfigModel? configModel)
                || configModel is null)
            {
                Console.WriteLine("""
                    Unable to load configuration,
                    Try to add it using:

                    sudo ./MonitoringAgent setup \
                    --server-url=https://monitoring.example.com \
                    --username=username \
                    --password=password \
                    --device-name=my-server

                    Or 

                    sudo ./MonitoringAgent setup \
                    --server-url=https://monitoring.example.com \
                    --device-id=a0318f0d-4bb2-45ee-b328-d8acf0f8d900 \
                    --api-key=2WHXP7tc0ohyU6yKeOTuRzO753iBkR0l

                    Note: to successfully send metrics the credentials 
                    need to be correct and stored in the main server database.
                    You can login/register by using the url of server in browser.

                    ---

                    If you already have an account, you can register this devices 
                    by providing unique name for this device, 
                    other information about the device will be provided automatically.

                    sudo ./MonitoringAgent register \
                    --server-url=https://monitoring.example.com \
                    --username=username \
                    --password=password \
                    --device-name=my-server

                    ---

                    You can also put anywhere flag '--interval=' 
                    which sets the interval between sending metrics messages (in seconds)
                    """);

                Console.WriteLine("Setup complete!"); // FIX - put somewhere else
                Console.WriteLine("Start the service: sudo systemctl start monitoring-agent");
                return;
            }

            // TRY to send metrics

            // if it fails try regenerating device id and api key - like on login 

            //await RunServiceAsync(args);
        }

        public static async Task TryCreateConfigFromArgsAsync(string[] args)
        {
            var options = ParseArgs(args.Skip(1).ToArray());

            if (options.TryGetValue("server-url", out var serverUrl) &&
                options.TryGetValue("username", out var username) &&
                options.TryGetValue("password", out var password) &&
                options.TryGetValue("device-name", out var deviceName))
            {
                if (serverUrl is null || username is null || password is null || deviceName is null)
                {
                    Console.WriteLine("Unable to find passed arguments");
                    return;
                }

                // POST /api/auth/login
                LoginModel? loginInfo = await _apiClientService.LoginAsync(username, password, serverUrl);

                if (loginInfo is null)
                {
                    Console.WriteLine("Unable to login using username and password");
                    return;
                }

                // GET /api/devices/regenerate-api-key?name= 
                DeviceModel? device = await _apiClientService.GetNewApiKeyByNameAsync(deviceName, loginInfo.Token, serverUrl);

                if (device is null)
                {
                    Console.WriteLine($"Unable to get info about device with name: {deviceName}");
                    return;
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


                // POST /api/devices/{deviceId}/metrics - if fails dont save configuration


                // Save config
                if (!ConfigService.TryToSaveFile(_configFile, config))
                {
                    return;
                }
            }

            if (options.TryGetValue("server-url", out var serverUri) &&
                options.TryGetValue("device-id", out var deviceId) &&
                options.TryGetValue("api-key", out var apiKey))
            {
                if (serverUrl is null || deviceId is null || apiKey is null)
                {
                    Console.WriteLine("Unable to find passed arguments");
                    return;
                }
                if (!Guid.TryParse(deviceId, out Guid id))
                {
                    Console.WriteLine("Unable to convert Device ID into proper form");
                    return;
                }


                // POST /api/devices/{deviceId}/metrics - if fails dont save configuration



                ConfigModel config = new ConfigModel()
                {
                    BaseUrl = serverUrl,
                    DeviceId = id,
                    ApiKey = apiKey
                };

                // Save config
                if (!ConfigService.TryToSaveFile(_configFile, config))
                {
                    return;
                }
            }
            else
                Console.WriteLine("Invalid arguments for setup command.");
        }

        public static async Task TryRegisterDeviceFromArgsAsync(string[] args)
        {
            var options = ParseArgs(args.Skip(1).ToArray());

            if (options.TryGetValue("server-url", out var serverUrl) &&
                options.TryGetValue("username", out var username) &&
                options.TryGetValue("password", out var password) &&
                options.TryGetValue("device-name", out var deviceName))
            {
                if (serverUrl is null || username is null || password is null || deviceName is null)
                {
                    Console.WriteLine("Unable to find passed arguments");
                    return;
                }

                // POST /api/auth/login
                LoginModel? loginInfo = await _apiClientService.LoginAsync(username, password, serverUrl);

                if (loginInfo is null)
                {
                    Console.WriteLine("Unable to login using username and password");
                    return;
                }

                // POST /api/devices - Create device
                DeviceModel device = _apiClientService.CreateDeviceAsync();

                // POST /api/devices/{deviceId}/metrics - if fails dont save configuration



            }
        }
        static Dictionary<string, string?> ParseArgs(string[] args)
        {
            var dict = new Dictionary<string, string?>();

            foreach (var arg in args)
            {
                if (!arg.StartsWith("--"))
                    continue;

                var parts = arg.Substring(2).Split('=', 2);

                var key = parts[0];
                string? value = parts.Length == 2 ? parts[1] : null;

                dict[key] = value;
            }
            return dict;
        }
    }
}
