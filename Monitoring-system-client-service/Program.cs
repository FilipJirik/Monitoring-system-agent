using Monitoring_system_agent.Models;
using Monitoring_system_agent.Services;
using Monitoring_system_client_service.Services;
using System.Runtime.InteropServices;

namespace Monitoring_system_client_service
{
    public class Program
    {
        private const string _configFile = "";
        public static async Task Main(string[] args)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine("This tool only works on Linux devices");
                return;
            }

            if (args.Length > 0)
            {
                var serviceCollection = new ServiceCollection();

                serviceCollection.AddHttpClient();
                serviceCollection.AddSingleton<ApiClientService>();
                serviceCollection.AddSingleton<SetupService>();

                var provider = serviceCollection.BuildServiceProvider();
                var setupService = provider.GetRequiredService<SetupService>();

                if (args[0] == "setup" || args[0] == "login")
                {
                    await setupService.RunSetupAsync(args);
                }
                else if (args[0] == "register")
                {
                    await setupService.RunCreateDeviceAsync(args);
                }
                else if (args[0] == "print-config")
                {
                    setupService.PrintConfig();
                }
                else
                    Console.WriteLine("Unknown command");

                return;
            }

            var builder = Host.CreateApplicationBuilder(args);
            
            builder.Services.AddSystemd(); 

            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<ApiClientService>();
            builder.Services.AddSingleton<LinuxMetricsService>(); 
            builder.Services.AddHostedService<Worker>(); 

            var host = builder.Build();
            await host.RunAsync();
        }
    }
}