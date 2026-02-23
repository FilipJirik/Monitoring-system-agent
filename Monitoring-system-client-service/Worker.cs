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
        if (!ValidateConfig())
            return;

        if (!Guid.TryParse(_config.DeviceId, out Guid deviceId))
        {
            _logger.LogCritical("Invalid DeviceId GUID. Run 'setup' or 'register' command first");
            return;
        }

        if (_config.AllowSelfSignedCertificates)
        {
            _logger.LogWarning("SSL certificate verification is DISABLED. Self-signed certificates will be accepted.");
        }

        _logger.LogInformation("Agent started - Device: {DeviceId}, Server: {BaseUrl}, Interval: {Interval}s",
            _config.DeviceId, _config.BaseUrl, _config.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectAndSendMetricsAsync(deviceId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Operation cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing metrics");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.IntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Agent stopped");
    }

    private bool ValidateConfig()
    {
        if (string.IsNullOrEmpty(_config.DeviceId) || string.IsNullOrEmpty(_config.ApiKey))
        {
            _logger.LogCritical("Configuration invalid. Run 'setup' or 'register' command first");
            return false;
        }
        return true;
    }

    private async Task CollectAndSendMetricsAsync(Guid deviceId, CancellationToken stoppingToken)
    {
        MetricsModel metrics = await _metricsService.GetMetricsAsync();

        bool success = await _apiClient.SendMetricsAsync(
            _config.BaseUrl, deviceId, _config.ApiKey, metrics);

        if (success)
        {
            _logger.LogDebug("Metrics sent - CPU: {Cpu}%, RAM: {Ram}MB",
                metrics.CpuUsagePercent, metrics.RamUsageMb);
        }
        else
        {
            _logger.LogWarning("Failed to send metrics");
        }
    }
}