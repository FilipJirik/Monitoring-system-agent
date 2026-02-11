using Microsoft.Extensions.Options;
using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using Monitoring_system_client_service.Services;

namespace Monitoring_system_client_service;

/// <summary>
/// Background worker service that periodically collects and sends metrics to the monitoring server.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ApiClientService _apiClient;
    private readonly LinuxMetricsService _metricsService;
    private readonly ConfigModel _config;

    /// <summary>
    /// Initializes a new instance of the Worker class.
    /// </summary>
    public Worker(ILogger<Worker> logger, ApiClientService apiClient, LinuxMetricsService metricsService, IOptions<ConfigModel> configOptions)
    {
        _logger = logger;
        _apiClient = apiClient;
        _metricsService = metricsService;
        _config = configOptions.Value;
    }

    /// <summary>
    /// Executes the background worker service logic.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Validate configuration
        if (!ValidateConfiguration())
        {
            return;
        }

        if (!Guid.TryParse(_config.DeviceId, out Guid deviceId))
        {
            _logger.LogCritical("DeviceId '{DeviceId}' is not a valid GUID. Please run 'setup' or 'register' command first.", _config.DeviceId);
            return;
        }

        LogStartupInformation(deviceId);

        // Main loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectAndSendMetricsAsync(deviceId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Metrics operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing metrics");
            }

            // Wait before next collection
            await WaitForNextCycleAsync(stoppingToken);
        }

        _logger.LogInformation("Worker service is stopping.");
    }

    /// <summary>
    /// Validates the configuration is properly set.
    /// </summary>
    private bool ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(_config.DeviceId) || string.IsNullOrEmpty(_config.ApiKey))
        {
            _logger.LogCritical("Configuration is invalid. Please run 'setup' or 'register' command first.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Logs startup information.
    /// </summary>
    private void LogStartupInformation(Guid deviceId)
    {
        _logger.LogInformation("Agent started for device: {DeviceId}", _config.DeviceId);
        _logger.LogInformation("Server URL: {ServerUrl}", _config.BaseUrl);
        _logger.LogInformation("Metrics collection interval: {Interval} seconds", _config.IntervalSeconds);
    }

    /// <summary>
    /// Collects metrics and sends them to the server.
    /// </summary>
    private async Task CollectAndSendMetricsAsync(Guid deviceId, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Collecting metrics...");
        MetricsModel metrics = await _metricsService.GetMetricsAsync();

        LogCollectedMetrics(metrics);

        _logger.LogInformation(
            "Sending metrics to server... CPU: {CpuUsage}%, NetIn: {NetworkIn} Kbps",
            metrics.CpuUsagePercent,
            metrics.NetworkInKbps);

        bool success = await _apiClient.SendMetricsAsync(
            _config.BaseUrl,
            deviceId,
            _config.ApiKey,
            metrics);

        if (!success)
        {
            _logger.LogWarning("Failed to send metrics to server.");
        }
        else
        {
            _logger.LogDebug("Metrics sent successfully.");
        }
    }

    /// <summary>
    /// Logs all collected metric values.
    /// </summary>
    private void LogCollectedMetrics(MetricsModel metrics)
    {
        _logger.LogDebug(
            "Metrics collected - CPU: {CpuUsage}%, RAM: {RamUsage}MB, NetIn: {NetworkIn} Kbps, NetOut: {NetworkOut} Kbps, Disk: {DiskUsage}%, Uptime: {Uptime}s",
            metrics.CpuUsagePercent,
            metrics.RamUsageMb,
            metrics.NetworkInKbps,
            metrics.NetworkOutKbps,
            metrics.DiskUsagePercent,
            metrics.UptimeSeconds);
    }

    /// <summary>
    /// Waits for the next metrics collection cycle.
    /// </summary>
    private async Task WaitForNextCycleAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Waiting {Interval} seconds before next metric collection...", _config.IntervalSeconds);
        await Task.Delay(TimeSpan.FromSeconds(_config.IntervalSeconds), stoppingToken);
    }
}