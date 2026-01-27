using Microsoft.Extensions.Options;
using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using Monitoring_system_client_service.Services;

namespace Monitoring_system_client_service;
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ApiClientService _apiClient;
    private readonly LinuxMetricsService _metricsService;
    private readonly ConfigModel _config;

    public Worker(ILogger<Worker> logger, ApiClientService apiClient, LinuxMetricsService metricsService, IOptions<ConfigModel> configOptions)
    {
        _logger = logger;
        _apiClient = apiClient;
        _metricsService = metricsService;
        _config = configOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_config.DeviceId) || string.IsNullOrEmpty(_config.ApiKey))
        {
            _logger.LogCritical("Configuration is invalid. Please run 'setup' or 'register' command first.");
            return;
        }

        if (!Guid.TryParse(_config.DeviceId, out Guid deviceId))
        {
            _logger.LogCritical("DeviceId '{DeviceId}' is not a valid GUID. Please run 'setup' or 'register' command first.", _config.DeviceId);
            return;
        }

        _logger.LogInformation("Agent started for device: {DeviceName}", _config.DeviceName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                MetricsModel metrics = await _metricsService.GetMetricsAsync();

                _logger.LogInformation(
                    "Sending metrics... CPU: {CpuUsage}%, NetIn: {NetworkIn} Kbps",
                    metrics.CpuUsagePercent,
                    metrics.NetworkInKbps);

                bool success = await _apiClient.SendMetricsAsync(
                    _config.BaseUrl,
                    deviceId,
                    _config.ApiKey,
                    metrics);

                if (!success)
                {
                    _logger.LogWarning("Failed to send metrics.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing metrics");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.IntervalSeconds), stoppingToken);
        }
    }
}