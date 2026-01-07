using Monitoring_system_agent.Models;

using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Monitoring_system_agent.Services
{
    public class ApiClientService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private const string _defaultbaseUrl = "https://localhost:8080/api";
        private const string _authUrl = "/auth/login";
        private const string _apiKeyUrl = "/devices/regenerate-api-key?name=";
        private const string _devicesUrl = "/api/devices";

        private const string _apiKeyHeader = "X-API-KEY";
        public ApiClientService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<LoginModel?> LoginAsync(string username, string password, string baseUrl = _defaultbaseUrl)
        {
            var httpClient = _httpClientFactory.CreateClient();

            RemoveLastSlash(ref baseUrl);

            var result = await httpClient.PostAsJsonAsync($"{baseUrl}{_authUrl}", new { username, password });

            if (!result.IsSuccessStatusCode)
            {
                return null;
            }

            var data = await result.Content.ReadFromJsonAsync<LoginModel>();
            return data;
        }

        public async Task<DeviceModel?> GetNewApiKeyByNameAsync(string deviceName, string jwtToken, string baseUrl = _defaultbaseUrl)
        {
            var httpClient = _httpClientFactory.CreateClient();

            RemoveLastSlash(ref baseUrl);

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer ", jwtToken);

            var result = await httpClient.GetAsync($"{baseUrl}{_apiKeyUrl}{deviceName}");

            if (!result.IsSuccessStatusCode)
            {
                return null;
            }

            return await result.Content.ReadFromJsonAsync<DeviceModel>();
        }

        public async Task<CreateDeviceModel?> CreateDeviceAsync(string name,
            string operatingSystem, string ipAddress, string macAddress,
            string baseUrl = _defaultbaseUrl)
        {
            var httpClient = _httpClientFactory.CreateClient();

            RemoveLastSlash(ref baseUrl);

            CreateDeviceModel model = new CreateDeviceModel()
            {
                Name = name,
                // TODO: get other attributes automatically
            };

            var result = await httpClient.PostAsJsonAsync($"{baseUrl}{_devicesUrl}", model);

            if (!result.IsSuccessStatusCode)
            {
                return null;
            }

            var data = await result.Content.ReadFromJsonAsync<CreateDeviceModel>();
            return data;
        }

        public async Task<bool> SendMetricsAsync(string baseUrl, Guid deviceId, string apiKey, MetricsModel metrics)
        {
            var httpClient = _httpClientFactory.CreateClient();

            RemoveLastSlash(ref baseUrl);

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add(_apiKeyHeader, apiKey);

            try
            {
                // POST /api/devices/{id}/metrics
                var response = await httpClient.PostAsJsonAsync($"{baseUrl}/devices/{deviceId}/metrics", metrics);

                if (!response.IsSuccessStatusCode)
                {
                    
                    var error = await response.Content.ReadAsStringAsync(); // for debugging
                    Console.WriteLine(error);
                }

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }


        private void RemoveLastSlash(ref string baseUrl)
        {
            if (baseUrl.EndsWith("/"))
            {
                baseUrl = baseUrl.Remove(baseUrl.Length - 1, 1);
            }
        }


    }
}
