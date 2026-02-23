using Monitoring_system_agent.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Monitoring_system_agent.Services;

public class ApiClientService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConfigModel _config;

    private const string _authUrl = "/auth/login";
    private const string _regenerateApiKeyUrl = "/regenerate-api-key";
    private const string _metricsUrl = "/metrics";
    private const string _devicesUrl = "/api/devices";
    private const string _jwtHeader = "Bearer";
    private const string _apiKeyHeader = "X-API-KEY";

    public ApiClientService(IHttpClientFactory httpClientFactory, IOptions<ConfigModel> configOptions)
    {
        _httpClientFactory = httpClientFactory;
        _config = configOptions.Value;
    }

    public async Task<LoginModel?> LoginAsync(string email, string password, string baseUrl)
    {
        var httpClient = CreateHttpClient();
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
        var httpClient = CreateHttpClient();
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

    public async Task<DeviceWithApiKeyModel?> CreateDeviceAsync(string name, string os, string ip, string mac, LoginModel loginInfo, string baseUrl)
    {
        var httpClient = CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_jwtHeader, loginInfo.Token);

        string endpoint = $"{NormalizeUrl(baseUrl)}{_devicesUrl}";

        try
        {
            var result = await httpClient.PostAsJsonAsync(endpoint, loginInfo);
            
            if (!result.IsSuccessStatusCode)
            {
                string errorContent = await result.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] CreateDevice failed with status {result.StatusCode}");
                Console.WriteLine($"[DEBUG] Error response: {errorContent}");
                Console.WriteLine($"[DEBUG] Request Body (LoginModel): {System.Text.Json.JsonSerializer.Serialize(loginInfo)}");
                return null;
            }

            return await result.Content.ReadFromJsonAsync<DeviceWithApiKeyModel>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to create device: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> SendMetricsAsync(string baseUrl, Guid deviceId, string apiKey, MetricsModel metrics)
    {
        var httpClient = CreateHttpClient();
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

    private HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };

        if (_config.AllowSelfSignedCertificates)
        {
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        }

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    private static string NormalizeUrl(string url)
    {
        return url.EndsWith("/") ? url[..^1] : url;
    }
}

