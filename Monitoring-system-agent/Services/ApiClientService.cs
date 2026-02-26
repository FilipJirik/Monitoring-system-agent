using Monitoring_system_client_service.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Monitoring_system_client_service.Services;

/// <summary>
/// Provides HTTP client functionality for communicating with the monitoring backend API.
/// Handles authentication, device registration, API key management, and metrics submission.
/// </summary>
public class ApiClientService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiClientService> _logger;

    // HTTP client factory name
    private const string DefaultHttpClientName = "default";

    // API endpoint routes
    private const string LoginEndpointPath = "/api/auth/login";
    private const string RegenerateApiKeyEndpointPath = "/regenerate-api-key";
    private const string MetricsEndpointPath = "/metrics";
    private const string DevicesEndpointPath = "/api/devices";

    // HTTP header schemes and values
    private const string BearerAuthenticationScheme = "Bearer";
    private const string ApiKeyHeaderName = "X-API-KEY";

    public ApiClientService(
        IHttpClientFactory httpClientFactory,
        ILogger<ApiClientService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and retrieves authentication tokens.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <param name="password">The user's password.</param>
    /// <param name="baseUrl">The base URL of the backend server.</param>
    /// <returns>
    /// A <see cref="LoginModel"/> containing authentication tokens if successful; null if authentication fails.
    /// </returns>
    public async Task<LoginModel?> LoginAsync(string email, string password, string baseUrl)
    {
        var httpClient = CreateHttpClient();
        string endpoint = $"{NormalizeUrl(baseUrl)}{LoginEndpointPath}";

        try
        {
            var response = await httpClient.PostAsJsonAsync(endpoint, new { email, password });
            if (response.IsSuccessStatusCode)
            {
                var loginData = await response.Content.ReadFromJsonAsync<LoginModel>();
                _logger.LogDebug("User {Email} authenticated successfully", email);
                return loginData;
            }

            _logger.LogWarning("Login failed for user {Email} with status code {StatusCode}", email, response.StatusCode);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during login attempt for user {Email}", email);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login to {Endpoint}", endpoint);
            return null;
        }
    }

    /// <summary>
    /// Retrieves/regenerates the API key for a device using JWT authentication.
    /// </summary>
    /// <param name="deviceId">The unique identifier of the device.</param>
    /// <param name="jwtToken">The JWT authentication token.</param>
    /// <param name="baseUrl">The base URL of the backend server.</param>
    /// <returns>
    /// A <see cref="DeviceWithApiKeyModel"/> containing the API key if successful; null if the request fails.
    /// </returns>
    public async Task<DeviceWithApiKeyModel?> GetApiKeyByIdAsync(Guid deviceId, string jwtToken, string baseUrl)
    {
        var httpClient = CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(BearerAuthenticationScheme, jwtToken);
        string endpoint = $"{NormalizeUrl(baseUrl)}{DevicesEndpointPath}/{deviceId}{RegenerateApiKeyEndpointPath}";

        try
        {
            var response = await httpClient.GetAsync(endpoint);
            if (response.IsSuccessStatusCode)
            {
                var deviceData = await response.Content.ReadFromJsonAsync<DeviceWithApiKeyModel>();
                _logger.LogDebug("API key retrieved for device {DeviceId}", deviceId);
                return deviceData;
            }

            _logger.LogWarning("Failed to retrieve API key for device {DeviceId} with status code {StatusCode}",
                deviceId, response.StatusCode);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error retrieving API key for device {DeviceId}", deviceId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving API key from {Endpoint}", endpoint);
            return null;
        }
    }

    /// <summary>
    /// Creates a new device registration on the backend using JWT authentication.
    /// </summary>
    /// <param name="device">The device information to register.</param>
    /// <param name="jwtToken">The JWT authentication token.</param>
    /// <param name="baseUrl">The base URL of the backend server.</param>
    /// <returns>
    /// A <see cref="DeviceWithApiKeyModel"/> containing the device details and API key if successful;
    /// null if the registration fails.
    /// </returns>
    public async Task<DeviceWithApiKeyModel?> CreateDeviceAsync(CreateDeviceModel device, string jwtToken, string baseUrl)
    {
        var httpClient = CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(BearerAuthenticationScheme, jwtToken);
        string endpoint = $"{NormalizeUrl(baseUrl)}{DevicesEndpointPath}";

        try
        {
            var response = await httpClient.PostAsJsonAsync(endpoint, device);

            if (response.IsSuccessStatusCode)
            {
                var deviceData = await response.Content.ReadFromJsonAsync<DeviceWithApiKeyModel>();
                _logger.LogInformation("Device {DeviceName} created successfully", device.Name);
                return deviceData;
            }

            string errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Device creation failed with status {StatusCode}: {ErrorResponse}",
                response.StatusCode, errorContent);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error creating device {DeviceName}", device.Name);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating device at {Endpoint}", endpoint);
            return null;
        }
    }

    /// <summary>
    /// Submits system metrics to the backend for a specific device.
    /// </summary>
    /// <param name="baseUrl">The base URL of the backend server.</param>
    /// <param name="deviceId">The unique identifier of the device.</param>
    /// <param name="apiKey">The API key for the device.</param>
    /// <param name="metrics">The system metrics to submit.</param>
    /// <returns>
    /// True if the metrics were submitted successfully; false otherwise.
    /// </returns>
    public async Task<bool> SendMetricsAsync(string baseUrl, Guid deviceId, string apiKey, MetricsModel metrics)
    {
        var httpClient = CreateHttpClient();
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add(ApiKeyHeaderName, apiKey);
        string endpoint = $"{NormalizeUrl(baseUrl)}{DevicesEndpointPath}/{deviceId}{MetricsEndpointPath}";

        try
        {
            var response = await httpClient.PostAsJsonAsync(endpoint, metrics);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Metrics submitted successfully for device {DeviceId}", deviceId);
                return true;
            }

            _logger.LogWarning("Metrics submission failed for device {DeviceId} with status code {StatusCode}",
                deviceId, response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error submitting metrics for device {DeviceId}", deviceId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error submitting metrics to {Endpoint}", endpoint);
            return false;
        }
    }

    /// <summary>
    /// Creates an HTTP client instance from the factory.
    /// </summary>
    /// <returns>A configured HTTP client.</returns>
    private HttpClient CreateHttpClient() => _httpClientFactory.CreateClient(DefaultHttpClientName);

    /// <summary>
    /// Removes trailing slashes from URLs to ensure consistent endpoint formatting.
    /// </summary>
    /// <param name="url">The URL to normalize.</param>
    /// <returns>The normalized URL without trailing slashes.</returns>
    private static string NormalizeUrl(string url) => url.TrimEnd('/');
}
