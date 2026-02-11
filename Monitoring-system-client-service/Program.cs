using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using Monitoring_system_client_service.CommandHandling;
using Monitoring_system_client_service.Extensions;
using Monitoring_system_client_service.Services;
using Monitoring_system_client_service.Validation;
using Tomlyn.Extensions.Configuration;

namespace Monitoring_system_client_service
{
    /// <summary>
    /// Main entry point for the Monitoring System Client Service.
    /// Handles both command-line operations and background monitoring service.
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                EnvironmentValidator.ThrowIfInvalidPlatform();

                if (args.Length > 0)
                {
                    await ExecuteCommandAsync(args);
                    return;
                }

                await RunMonitoringServiceAsync(args);
            }
            catch (PlatformNotSupportedException ex)
            {
                Console.Error.WriteLine($"[ERROR] Platform not supported: {ex.Message}");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FATAL] Unhandled error: {ex.Message}");
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    Console.Error.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                }
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Executes a command-line command.
        /// </summary>
        private static async Task ExecuteCommandAsync(string[] args)
        {
            Console.WriteLine("[DEBUG] Initializing command execution mode...");
            
            var services = new ServiceCollection();
            services.AddSetupServices();

            using var serviceProvider = services.BuildServiceProvider();
            var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();

            Console.WriteLine($"[DEBUG] Executing command: {args[0]}");
            await commandHandler.ExecuteAsync(args);
        }

        /// <summary>
        /// Runs the monitoring service as a background worker.
        /// </summary>
        private static async Task RunMonitoringServiceAsync(string[] args)
        {
            Console.WriteLine("[INFO] Initializing monitoring service...");
            
            ValidateConfigurationExists();

            var builder = Host.CreateApplicationBuilder(args);

            Console.WriteLine("[DEBUG] Configuring services...");
            ConfigureServices(builder);

            var host = builder.Build();
            
            Console.WriteLine("[INFO] Starting monitoring service...");
            await host.RunAsync();
        }

        /// <summary>
        /// Validates that the configuration file exists.
        /// </summary>
        private static void ValidateConfigurationExists()
        {
            if (!File.Exists(ConfigService.FileName))
            {
                throw new FileNotFoundException(
                    $"Configuration file '{ConfigService.FileName}' not found. " +
                    $"Please run 'setup' or 'register' command first.");
            }
            Console.WriteLine($"[DEBUG] Configuration file found: {ConfigService.FileName}");
        }

        /// <summary>
        /// Configures host services and logging.
        /// </summary>
        private static void ConfigureServices(HostApplicationBuilder builder)
        {
            // Add systemd integration for Linux service management
            builder.Services.AddSystemd();

            // Add monitoring-specific services
            builder.Services.AddMonitoringServices();

            // Configure application configuration
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddTomlFile(
                ConfigService.FileName,
                optional: false,
                reloadOnChange: true);

            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddSystemdConsole();

            Console.WriteLine("[DEBUG] Services and logging configured");
        }
    }
}