using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using Monitoring_system_client_service.Services;
using System.Runtime.InteropServices;
using Tomlyn.Extensions.Configuration;

namespace Monitoring_system_client_service
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine("This tool only works on Linux devices");
                return;
            }

            if (args.Length > 0)
            {
                await HandleCommandAsync(args);
                return;
            }

            if (!File.Exists(ConfigService.FileName))
            {
                Console.WriteLine($"Configuration file '{ConfigService.FileName}' not found.");
                Console.WriteLine("Please run 'setup' or 'register' command first.");
                return;
            }

            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddSystemd();

            builder.Configuration.Sources.Clear();
            builder.Configuration.AddTomlFile(ConfigService.FileName, optional: false, reloadOnChange: true);

            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<ApiClientService>();
            builder.Services.AddSingleton<LinuxMetricsService>();
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            await host.RunAsync();
        }

        private static async Task HandleCommandAsync(string[] args)
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddHttpClient();
            serviceCollection.AddSingleton<ApiClientService>();
            serviceCollection.AddSingleton<SetupService>();

            var provider = serviceCollection.BuildServiceProvider();
            var setupService = provider.GetRequiredService<SetupService>();

            string command = args[0];

            switch (command)
            {
                case "setup":
                case "login":
                    await setupService.RunSetupAsync(args);
                    break;
                case "register":
                    await setupService.RunCreateDeviceAsync(args);
                    break;
                case "print-config":
                    setupService.PrintConfig();
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    break;
            }
        }
    }
}