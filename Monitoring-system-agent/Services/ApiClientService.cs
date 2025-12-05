using Monitoring_system_agent.Models;
using System.Buffers.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Monitoring_system_agent.Services
{
    internal class ApiClientService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string _baseUrl = "https://localhost:8080/api";
        private const string _authUrl = "/auth/login";
        private const string _apiKeyUrl = "/devices/regenerate-api-key?name=";
        public ApiClientService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<LoginModel?> LoginAsync(string username, string password, string baseUrl = _baseUrl)
        {
            var httpClient = _httpClientFactory.CreateClient();

            RemoveLastSlash(baseUrl);

            var result = await httpClient.PostAsJsonAsync($"{baseUrl}/{_authUrl}", new { username, password });

            if (!result.IsSuccessStatusCode)
            {
                return null;
            }

            var data = await result.Content.ReadFromJsonAsync<LoginModel>();
            return data;
        }

        public async Task<DeviceModel?> GetNewApiKeyByNameAsync(string deviceName, string jwtToken, string baseUrl = _baseUrl)
        {
            var httpClient = _httpClientFactory.CreateClient();

            RemoveLastSlash(baseUrl);

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer ", jwtToken);

            var result = await httpClient.GetAsync($"{baseUrl}/{_apiKeyUrl}{deviceName}");

            if (!result.IsSuccessStatusCode)
            {
                return null;
            }

            return await result.Content.ReadFromJsonAsync<DeviceModel>();
        }

        public async Task<CreateDeviceModel?> CreateDeviceAsync(string name,
            string operatingSystem, string ipAddress, string macAddress, 
            string baseUrl = _baseUrl)
        {
            var httpClient = _httpClientFactory.CreateClient();

            RemoveLastSlash(baseUrl);

            var result = await httpClient.PostAsJsonAsync($"{baseUrl}/{_authUrl}", new { username, password });

            if (!result.IsSuccessStatusCode)
            {
                return null;
            }

            var data = await result.Content.ReadFromJsonAsync<LoginModel>();
            return data;
        }


        private void RemoveLastSlash(string baseUrl)
        {
            if (baseUrl.EndsWith("/"))
            {
                baseUrl = baseUrl.Remove(baseUrl.Length - 1, 1);
            }
        }


    }
}
