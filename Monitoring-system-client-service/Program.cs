using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using Monitoring_system_client_service.CommandHandling;
using Monitoring_system_client_service.Extensions;
using Monitoring_system_client_service.Services;
using Tomlyn.Extensions.Configuration;

namespace Monitoring_system_client_service;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            if (args.Length > 0)
                await ExecuteCommandAsync(args);
            else
                await RunMonitoringServiceAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[CRITICAL] {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task ExecuteCommandAsync(string[] args)
    {
        var services = new ServiceCollection();

        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        services.AddHttpClient();
        services.AddSingleton<ApiClientService>();
        services.AddSingleton<SetupService>();
        services.AddSingleton<CommandHandler>();

        using var serviceProvider = services.BuildServiceProvider();
        var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();

        try
        {
            await commandHandler.ExecuteCommandAsync(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Command failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task RunMonitoringServiceAsync()
    {
        if (!File.Exists(ConfigService.FileName))
        {
            Console.Error.WriteLine($"[ERROR] Configuration file '{ConfigService.FileName}' not found");
            Console.Error.WriteLine("[ERROR] Run 'setup' or 'register' command first");
            Environment.Exit(1);
        }

        var builder = Host.CreateApplicationBuilder();
        
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddTomlFile(ConfigService.FileName, optional: false, reloadOnChange: true);
        
        // Configure TOML snake_case to C# PascalCase property binding
        builder.Services.Configure<ConfigModel>(options =>
        {
            var section = builder.Configuration;
            if (section["base_url"] is string baseUrl)
                options.BaseUrl = baseUrl;
            if (section["device_id"] is string deviceId)
                options.DeviceId = deviceId;
            if (section["api_key"] is string apiKey)
                options.ApiKey = apiKey;
            if (section["interval_seconds"] is string intervalStr && int.TryParse(intervalStr, out int interval))
                options.IntervalSeconds = interval;
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<ApiClientService>();
        builder.Services.AddSingleton<LinuxMetricsService>();
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IOptions<ConfigModel>>().Value;
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Configuration loaded - Server: {BaseUrl}, Device: {DeviceId}",
            config.BaseUrl, config.DeviceId);

        await host.RunAsync();
    }
}