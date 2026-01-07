using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using Monitoring_system_client_service.Services;

namespace Monitoring_system_client_service;
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ApiClientService _apiClient;
    private readonly LinuxMetricsService _metricsService;

    private const string _configFile = "agent_config.json";
    private ConfigModel? _config;

    public Worker(ILogger<Worker> logger, ApiClientService apiClient, LinuxMetricsService metricsService)
    {
        _logger = logger;
        _apiClient = apiClient;
        _metricsService = metricsService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ConfigService.TryLoadConfig(_configFile, out _config) || _config is null)
        {
            _logger.LogCritical("Configuration file not found or invalid. Please run 'setup' first.");
            return;
        }

        _logger.LogInformation($"Agent started for device: {_config.DeviceName}");


        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                MetricsModel metrics = await _metricsService.GetMetricsAsync();

                _logger.LogInformation($"Sending metrics... CPU: {metrics.CpuUsagePercent}%, NetIn: {metrics.NetworkInKbps} Kbps");

                bool success = await _apiClient.SendMetricsAsync(
                    _config.BaseUrl,
                    _config.DeviceId,
                    _config.ApiKey,
                    metrics);

                if (!success) _logger.LogWarning("Failed to send metrics.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loop");
            }

            await Task.Delay(_config.Interval, stoppingToken);
        }
    }
}