using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using Monitoring_system_client_service.CommandHandling;
using Monitoring_system_client_service.Extensions;
using Monitoring_system_client_service.Services;
using Monitoring_system_client_service.Validation;
using Tomlyn.Extensions.Configuration;


namespace Monitoring_system_client_service
{
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
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task ExecuteCommandAsync(string[] args)
        {
            var services = new ServiceCollection();
            services.AddSetupServices();

            using var serviceProvider = services.BuildServiceProvider();
            var commandHandler = new CommandHandler(serviceProvider.GetRequiredService<SetupService>());

            await commandHandler.ExecuteAsync(args);
        }

        private static async Task RunMonitoringServiceAsync(string[] args)
        {
            if (!File.Exists(ConfigService.FileName))
            {
                throw new FileNotFoundException(
                    $"Configuration file '{ConfigService.FileName}' not found. " +
                    $"Please run 'setup' or 'register' command first.");
            }

            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddSystemd();
            builder.Services.AddMonitoringServices();

            builder.Configuration.Sources.Clear();
            builder.Configuration.AddTomlFile(ConfigService.FileName, optional: false, reloadOnChange: true);

            var host = builder.Build();
            await host.RunAsync();
        }
    }
}