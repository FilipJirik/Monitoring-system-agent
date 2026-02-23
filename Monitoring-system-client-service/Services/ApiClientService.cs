using Monitoring_system_client_service.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Monitoring_system_client_service.Services;

public class ApiClientService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiClientService> _logger;

    private const string HttpClientName = "default";
    private const string AuthEndpoint = "/auth/login";
    private const string RegenerateApiKeyEndpoint = "/regenerate-api-key";
    private const string MetricsEndpoint = "/metrics";
    private const string DevicesEndpoint = "/api/devices";
    private const string BearerScheme = "Bearer ";
    private const string ApiKeyHeader = "X-API-KEY";

    public ApiClientService(
        IHttpClientFactory httpClientFactory,
        ILogger<ApiClientService> logger,
        IOptions<ConfigModel> configOptions)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<LoginModel?> LoginAsync(string email, string password, string baseUrl)
    {
        var httpClient = CreateHttpClient();
        string endpoint = $"{NormalizeUrl(baseUrl)}{AuthEndpoint}";

        try
        {
            var result = await httpClient.PostAsJsonAsync(endpoint, new { email, password });
            return result.IsSuccessStatusCode
                ? await result.Content.ReadFromJsonAsync<LoginModel>()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login request to {Endpoint} failed", endpoint);
            return null;
        }
    }

    public async Task<DeviceWithApiKeyModel?> GetApiKeyByIdAsync(Guid deviceId, string jwtToken, string baseUrl)
    {
        var httpClient = CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(BearerScheme, jwtToken);
        string endpoint = $"{NormalizeUrl(baseUrl)}{DevicesEndpoint}/{deviceId}{RegenerateApiKeyEndpoint}";

        try
        {
            var result = await httpClient.GetAsync(endpoint);
            return result.IsSuccessStatusCode
                ? await result.Content.ReadFromJsonAsync<DeviceWithApiKeyModel>()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get API key for device {DeviceId}", deviceId);
            return null;
        }
    }

    public async Task<DeviceWithApiKeyModel?> CreateDeviceAsync(CreateDeviceModel device, string jwtToken, string baseUrl)
    {
        var httpClient = CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(BearerScheme, jwtToken);
        string endpoint = $"{NormalizeUrl(baseUrl)}{DevicesEndpoint}";

        try
        {
            var result = await httpClient.PostAsJsonAsync(endpoint, device);

            if (!result.IsSuccessStatusCode)
            {
                string errorContent = await result.Content.ReadAsStringAsync();
                _logger.LogDebug("CreateDevice failed — Status: {StatusCode}, Response: {Response}",
                    result.StatusCode, errorContent);
                return null;
            }

            return await result.Content.ReadFromJsonAsync<DeviceWithApiKeyModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create device at {Endpoint}", endpoint);
            return null;
        }
    }

    public async Task<bool> SendMetricsAsync(string baseUrl, Guid deviceId, string apiKey, MetricsModel metrics)
    {
        var httpClient = CreateHttpClient();
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add(ApiKeyHeader, apiKey);
        string endpoint = $"{NormalizeUrl(baseUrl)}{DevicesEndpoint}/{deviceId}{MetricsEndpoint}";

        try
        {
            var response = await httpClient.PostAsJsonAsync(endpoint, metrics);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send metrics for device {DeviceId}", deviceId);
            return false;
        }
    }

    private HttpClient CreateHttpClient() => _httpClientFactory.CreateClient(HttpClientName);

    private static string NormalizeUrl(string url) => url.TrimEnd('/');
}
