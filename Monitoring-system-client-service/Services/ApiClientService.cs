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

        var result = await httpClient.PostAsJsonAsync($"{baseUrl}{_authUrl}", new { email, password });

        if (!result.IsSuccessStatusCode)
            return null;

        return await result.Content.ReadFromJsonAsync<LoginModel>();
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

        var result = await httpClient.GetAsync($"{baseUrl}{_regenerateApiKeyUrl}{deviceName}");

        if (!result.IsSuccessStatusCode)
            return null;

        return await result.Content.ReadFromJsonAsync<DeviceModel>();
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

        var result = await httpClient.GetAsync($"{baseUrl}{_devicesUrl}/{deviceId}{_regenerateApiKeyUrl}");

        if (!result.IsSuccessStatusCode)
            return null;

        return await result.Content.ReadFromJsonAsync<DeviceModel>();
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

        var result = await httpClient.PostAsJsonAsync($"{baseUrl}{_devicesUrl}", createModel);

        if (!result.IsSuccessStatusCode) 
            return null;

        return await result.Content.ReadFromJsonAsync<DeviceModel>();
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

        try
        {
            var response = await httpClient.PostAsJsonAsync($"{baseUrl}{_devicesUrl}/{deviceId}{_metricsUrl}", metrics);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine(error);
            }

            return response.IsSuccessStatusCode;
        }
        catch
        {
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

