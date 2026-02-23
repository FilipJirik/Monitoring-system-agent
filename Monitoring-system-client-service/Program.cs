using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting.Systemd;
using Monitoring_system_client_service.Configuration;
using Monitoring_system_client_service.Models;
using Monitoring_system_client_service.Services;
using Monitoring_system_client_service.CommandHandling;
using System.Runtime.InteropServices;
using Tomlyn.Extensions.Configuration;

namespace Monitoring_system_client_service;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Console.Error.WriteLine($"[CRITICAL] This program only works on linux machines.");
            Environment.Exit(1);
        }
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
        if (!ConfigService.ValidateConfigFileExists())
            Environment.Exit(1);

        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddTomlFile(ConfigService.FileName, optional: false, reloadOnChange: true);

        // Add configuration from TOML file
        builder.Services.Configure<ConfigModel>(builder.Configuration);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // Configure HttpClient with self-signed certificate 
        builder.Services.AddHttpClient("default")
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var config = sp.GetRequiredService<IOptions<ConfigModel>>().Value;
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };

                if (config.AllowSelfSignedCertificates)
                {
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                }

                return handler;
            });

        builder.Services.AddSingleton<ApiClientService>();
        builder.Services.AddSingleton<LinuxMetricsService>();
        builder.Services.AddHostedService<Worker>();

        // Add Systemd support for journald logging
        builder.Services.AddSystemd();

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IOptions<ConfigModel>>().Value;
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Configuration loaded - Server: {BaseUrl}, Device: {DeviceId}",
            config.BaseUrl, config.DeviceId);

        await host.RunAsync();
    }
}