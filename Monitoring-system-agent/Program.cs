using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting.Systemd;
using Monitoring_system_client_service.Configuration;
using Monitoring_system_client_service.Models;
using Monitoring_system_client_service.Services;
using Monitoring_system_client_service.CommandHandling;
using System.Runtime.InteropServices;
using Tomlyn.Extensions.Configuration;

namespace Monitoring_system_client_service;

/// <summary>
/// Entry point for the monitoring agent application.
/// Supports both CLI mode (for configuration) and daemon mode (for metrics collection).
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point of the application.
    /// Verifies Linux platform, then either executes a CLI command or runs the monitoring daemon.
    /// </summary>
    /// <param name="args">Command-line arguments for the application.</param>
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

    /// <summary>
    /// Executes a CLI command (setup, register, print-config, or help).
    /// </summary>
    /// <param name="args">The command and its arguments.</param>
    private static async Task ExecuteCommandAsync(string[] args)
    {
        bool allowSelfSigned = ResolveSelfSignedCertificateFlag(args);
        using var serviceProvider = BuildCliServiceProvider(allowSelfSigned);

        var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();
        await commandHandler.ExecuteCommandAsync(args);
    }

    /// <summary>
    /// Builds a DI service provider for CLI command execution.
    /// </summary>
    /// <param name="allowSelfSigned">Whether to allow self-signed SSL certificates.</param>
    /// <returns>A configured <see cref="ServiceProvider"/> ready for CLI use.</returns>
    private static ServiceProvider BuildCliServiceProvider(bool allowSelfSigned)
    {
        var services = new ServiceCollection();

        var tempConfig = new ConfigurationBuilder()
            .AddTomlFile(ConfigService.FileName, optional: true)
            .Build();

        services.Configure<ConfigModel>(tempConfig);

        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        ConfigureHttpClient(services, allowSelfSigned);

        services.AddSingleton<ApiClientService>();
        services.AddSingleton<SetupService>();
        services.AddSingleton<CommandHandler>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Resolves whether self-signed certificates should be accepted for CLI commands.
    /// CLI argument takes priority; falls back to the value from the configuration file.
    /// </summary>
    /// <param name="args">The raw CLI arguments.</param>
    /// <returns>True if self-signed certificates should be accepted.</returns>
    private static bool ResolveSelfSignedCertificateFlag(string[] args)
    {
        var options = CliParser.Parse(args.Skip(1).ToArray());

        if (options.TryGetValue("allow-self-signed-certificates", out var certStr) &&
            bool.TryParse(certStr, out bool allow))
        {
            return allow;
        }

        // Fall back to config file value (defaults to true if no config exists)
        var tempConfig = new ConfigurationBuilder()
            .AddTomlFile(ConfigService.FileName, optional: true)
            .Build();

        var configModel = new ConfigModel();
        tempConfig.Bind(configModel);

        return configModel.AllowSelfSignedCertificates;
    }

    /// <summary>
    /// Runs the monitoring daemon service for continuous metrics collection and submission.
    /// Requires a valid configuration file to exist.
    /// </summary>
    private static async Task RunMonitoringServiceAsync()
    {
        if (!ConfigService.ValidateConfigFileExists())
            Environment.Exit(1);

        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddTomlFile(ConfigService.FileName, optional: false, reloadOnChange: true);

        builder.Services.Configure<ConfigModel>(builder.Configuration);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        ConfigureHttpClient(builder.Services);

        builder.Services.AddSingleton<ApiClientService>();
        builder.Services.AddSingleton<LinuxMetricsService>();
        builder.Services.AddHostedService<Worker>();

        builder.Services.AddSystemd();

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IOptions<ConfigModel>>().Value;
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Configuration loaded - Server: {BaseUrl}, Device: {DeviceId}",
            config.BaseUrl, config.DeviceId);

        await host.RunAsync();
    }

    /// <summary>
    /// Configures the named HTTP client with optional self-signed certificate support.
    /// When <paramref name="allowSelfSignedOverride"/> is provided (CLI mode), it is used directly.
    /// Otherwise (daemon mode), the value is resolved from the bound configuration at runtime.
    /// </summary>
    /// <param name="services">The service collection to add the HTTP client to.</param>
    /// <param name="allowSelfSignedOverride">
    /// Explicit override for self-signed certificate acceptance (used in CLI mode).
    /// When null, the decision is deferred to the runtime configuration.
    /// </param>
    private static void ConfigureHttpClient(IServiceCollection services, bool? allowSelfSignedOverride = null)
    {
        services.AddHttpClient("default")
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };

                bool shouldAllowSelfSigned = allowSelfSignedOverride
                    ?? sp.GetRequiredService<IOptions<ConfigModel>>().Value.AllowSelfSignedCertificates;

                if (shouldAllowSelfSigned)
                {
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                }

                return handler;
            });
    }
}