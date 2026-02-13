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
    private const string _jwtHeader = "Bearer";
    private const string _apiKeyHeader = "X-API-KEY";

    public ApiClientService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<LoginModel?> LoginAsync(string email, string password, string baseUrl)
    {
        var httpClient = _httpClientFactory.CreateClient();
        string endpoint = $"{NormalizeUrl(baseUrl)}{_authUrl}";

        try
        {
            var result = await httpClient.PostAsJsonAsync(endpoint, new { email, password });
            return result.IsSuccessStatusCode
                ? await result.Content.ReadFromJsonAsync<LoginModel>()
                : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Login failed: {ex.Message}");
            return null;
        }
    }

    public async Task<DeviceWithApiKeyModel?> GetApiKeyByIdAsync(Guid deviceId, string jwtToken, string baseUrl)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_jwtHeader, jwtToken);
        string endpoint = $"{NormalizeUrl(baseUrl)}{_devicesUrl}/{deviceId}{_regenerateApiKeyUrl}";

        try
        {
            var result = await httpClient.GetAsync(endpoint);
            return result.IsSuccessStatusCode
                ? await result.Content.ReadFromJsonAsync<DeviceWithApiKeyModel>()
                : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to get API key: {ex.Message}");
            return null;
        }
    }

    public async Task<DeviceWithApiKeyModel?> CreateDeviceAsync(string name, string os, string ip, string mac, string jwtToken, string baseUrl)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_jwtHeader, jwtToken);

        var createModel = new CreateDeviceModel
        {
            Name = name,
            OperatingSystem = os,
            IpAddress = ip,
            MacAddress = mac
        };

        string endpoint = $"{NormalizeUrl(baseUrl)}{_devicesUrl}";

        try
        {
            var result = await httpClient.PostAsJsonAsync(endpoint, createModel);
            return result.IsSuccessStatusCode
                ? await result.Content.ReadFromJsonAsync<DeviceWithApiKeyModel>()
                : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to create device: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> SendMetricsAsync(string baseUrl, Guid deviceId, string apiKey, MetricsModel metrics)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add(_apiKeyHeader, apiKey);

        string endpoint = $"{NormalizeUrl(baseUrl)}{_devicesUrl}/{deviceId}{_metricsUrl}";

        try
        {
            var response = await httpClient.PostAsJsonAsync(endpoint, metrics);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to send metrics: {ex.Message}");
            return false;
        }
    }

    private static string NormalizeUrl(string url)
    {
        return url.EndsWith("/") ? url[..^1] : url;
    }
}

