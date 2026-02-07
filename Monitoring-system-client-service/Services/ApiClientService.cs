using Monitoring_system_agent.Models;

using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Monitoring_system_agent.Services;

public class ApiClientService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private const string _authUrl = "/auth/login";
    private const string _regenerateApiKeyUrl = "/regenerate-api-key";
    private const string _metricsUrl = "/metrics";
    private const string _devicesUrl = "/api/devices";

    private const string _jwtHeader = "Bearer ";
    private const string _apiKeyHeader = "X-API-KEY"; // FIXME: change to modern standard
    public ApiClientService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Authenticates with the API using email and password, and retrieves a JWT token.
    /// </summary>
    /// <param name="email">The user's email address</param>
    /// <param name="password">The user's password</param>
    /// <param name="baseUrl">The base URL of the API server</param>
    /// <returns>A LoginModel containing the JWT token if successful, null otherwise</returns>
    public async Task<LoginModel?> LoginAndGetTokenAsync(string email, string password, string baseUrl)
    {
        var httpClient = _httpClientFactory.CreateClient();

        RemoveLastSlash(ref baseUrl);

        string endpoint = $"{baseUrl}{_authUrl}";
        Console.WriteLine($"[DEBUG] Attempting login...");
        Console.WriteLine($"[DEBUG] Endpoint: POST {endpoint}");
        Console.WriteLine($"[DEBUG] Email: {email}");

        try
        {
            var result = await httpClient.PostAsJsonAsync(endpoint, new { email, password });

            Console.WriteLine($"[DEBUG] Login response status: {result.StatusCode}");

            if (!result.IsSuccessStatusCode)
            {
                var errorContent = await result.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] Login failed. Error: {errorContent}");
                return null;
            }

            var loginData = await result.Content.ReadFromJsonAsync<LoginModel>();
            Console.WriteLine($"[DEBUG] Login successful. Token received (length: {loginData?.Token?.Length ?? 0} characters)");
            return loginData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Login exception: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Retrieves a new API key for a device by its name using a JWT token for authentication.
    /// </summary>
    /// <param name="deviceName">The name of the device</param>
    /// <param name="jwtToken">The JWT authentication token</param>
    /// <param name="baseUrl">The base URL of the API server</param>
    /// <returns>A DeviceModel containing the device information and API key if successful, null otherwise</returns>
    public async Task<DeviceModel?> GetNewApiKeyByNameAsync(string deviceName, string jwtToken, string baseUrl)
    {
        var httpClient = _httpClientFactory.CreateClient();

        RemoveLastSlash(ref baseUrl);

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_jwtHeader, jwtToken);

        string endpoint = $"{baseUrl}{_devicesUrl}?name={deviceName}{_regenerateApiKeyUrl}";
        Console.WriteLine($"[DEBUG] Attempting to regenerate API key by device name...");
        Console.WriteLine($"[DEBUG] Endpoint: GET {endpoint}");
        Console.WriteLine($"[DEBUG] Device name: {deviceName}");

        try
        {
            var result = await httpClient.GetAsync(endpoint);

            Console.WriteLine($"[DEBUG] API key regeneration response status: {result.StatusCode}");

            if (!result.IsSuccessStatusCode)
            {
                var errorContent = await result.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] API key regeneration failed. Error: {errorContent}");
                return null;
            }

            var deviceData = await result.Content.ReadFromJsonAsync<DeviceModel>();
            Console.WriteLine($"[DEBUG] API key regeneration successful. Device ID: {deviceData?.Id}");
            return deviceData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] API key regeneration exception: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Retrieves a new API key for a device by its ID using a JWT token for authentication.
    /// </summary>
    /// <param name="deviceId">The unique identifier of the device</param>
    /// <param name="jwtToken">The JWT authentication token</param>
    /// <param name="baseUrl">The base URL of the API server</param>
    /// <returns>A DeviceModel containing the device information and API key if successful, null otherwise</returns>
    public async Task<DeviceModel?> GetNewApiKeyByIdAsync(Guid deviceId, string jwtToken, string baseUrl)
    {
        var httpClient = _httpClientFactory.CreateClient();

        RemoveLastSlash(ref baseUrl);

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_jwtHeader, jwtToken);

        string endpoint = $"{baseUrl}{_devicesUrl}/{deviceId}{_regenerateApiKeyUrl}";
        Console.WriteLine($"[DEBUG] Attempting to regenerate API key by device ID...");
        Console.WriteLine($"[DEBUG] Endpoint: GET {endpoint}");
        Console.WriteLine($"[DEBUG] Device ID: {deviceId}");

        try
        {
            var result = await httpClient.GetAsync(endpoint);

            Console.WriteLine($"[DEBUG] API key regeneration response status: {result.StatusCode}");

            if (!result.IsSuccessStatusCode)
            {
                var errorContent = await result.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] API key regeneration failed. Error: {errorContent}");
                return null;
            }

            var deviceData = await result.Content.ReadFromJsonAsync<DeviceModel>();
            Console.WriteLine($"[DEBUG] API key regeneration successful. Device ID: {deviceData?.Id}");
            return deviceData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] API key regeneration exception: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Creates a new device registration on the API server using a JWT token for authentication.
    /// </summary>
    /// <param name="name">The name of the device</param>
    /// <param name="os">The operating system description</param>
    /// <param name="ip">The IP address of the device</param>
    /// <param name="mac">The MAC address of the device</param>
    /// <param name="jwtToken">The JWT authentication token</param>
    /// <param name="baseUrl">The base URL of the API server</param>
    /// <returns>A DeviceModel containing the created device information if successful, null otherwise</returns>
    public async Task<DeviceModel?> CreateDeviceAsync(string name, string os, string ip, string mac, string jwtToken, string baseUrl)
    {
        var httpClient = _httpClientFactory.CreateClient();

        RemoveLastSlash(ref baseUrl);

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_jwtHeader, jwtToken);

        var createModel = new CreateDeviceModel
        {
            Name = name,
            OperatingSystem = os,
            IpAddress = ip,
            MacAddress = mac
        };

        string endpoint = $"{baseUrl}{_devicesUrl}";
        Console.WriteLine($"[DEBUG] Attempting to create new device...");
        Console.WriteLine($"[DEBUG] Endpoint: POST {endpoint}");
        Console.WriteLine($"[DEBUG] Device name: {name}");
        Console.WriteLine($"[DEBUG] Device OS: {os}");
        Console.WriteLine($"[DEBUG] Device IP: {ip}");
        Console.WriteLine($"[DEBUG] Device MAC: {mac}");

        try
        {
            var result = await httpClient.PostAsJsonAsync(endpoint, createModel);

            Console.WriteLine($"[DEBUG] Device creation response status: {result.StatusCode}");

            if (!result.IsSuccessStatusCode)
            {
                var errorContent = await result.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] Device creation failed. Error: {errorContent}");
                return null;
            }

            var deviceData = await result.Content.ReadFromJsonAsync<DeviceModel>();
            Console.WriteLine($"[DEBUG] Device creation successful. Device ID: {deviceData?.Id}, Device name: {deviceData?.Name}");
            return deviceData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Device creation exception: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Sends device metrics to the API server using an API key for authentication.
    /// </summary>
    /// <param name="baseUrl">The base URL of the API server</param>
    /// <param name="deviceId">The unique identifier of the device</param>
    /// <param name="apiKey">The API key for authentication</param>
    /// <param name="metrics">The metrics data to send</param>
    /// <returns>True if the metrics were sent successfully, false otherwise</returns>
    public async Task<bool> SendMetricsAsync(string baseUrl, Guid deviceId, string apiKey, MetricsModel metrics)
    {
        var httpClient = _httpClientFactory.CreateClient();

        RemoveLastSlash(ref baseUrl);

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add(_apiKeyHeader, apiKey);

        string endpoint = $"{baseUrl}{_devicesUrl}/{deviceId}{_metricsUrl}";
        Console.WriteLine($"[DEBUG] Sending metrics to server...");
        Console.WriteLine($"[DEBUG] Endpoint: POST {endpoint}");
        Console.WriteLine($"[DEBUG] Device ID: {deviceId}");
        Console.WriteLine($"[DEBUG] Metrics - CPU: {metrics.CpuUsagePercent}%, RAM: {metrics.RamUsageMb}MB, Net In: {metrics.NetworkInKbps} Kbps, Net Out: {metrics.NetworkOutKbps} Kbps");

        try
        {
            var response = await httpClient.PostAsJsonAsync(endpoint, metrics);

            Console.WriteLine($"[DEBUG] Metrics send response status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] Metrics send failed. Error: {errorContent}");
                return false;
            }

            Console.WriteLine($"[DEBUG] Metrics sent successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Metrics send exception: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Removes the last slash at the end of the URL if present.
    /// </summary>
    /// <param name="baseUrl">The URL to process</param>
    private void RemoveLastSlash(ref string baseUrl)
    {
        if (baseUrl.EndsWith("/"))
        {
            baseUrl = baseUrl.Remove(baseUrl.Length - 1, 1);
        }
    }


}

