using Monitoring_system_agent.Services;
using Monitoring_system_client_service.CommandHandling;
using Monitoring_system_client_service.Services;

namespace Monitoring_system_client_service.Extensions
{
    /// <summary>
    /// Extension methods for service collection configuration.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers services required for monitoring operations.
        /// </summary>
        public static IServiceCollection AddMonitoringServices(this IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddSingleton<ApiClientService>();
            services.AddSingleton<LinuxMetricsService>();
            services.AddHostedService<Worker>();

            return services;
        }

        /// <summary>
        /// Registers services required for setup and command handling operations.
        /// </summary>
        public static IServiceCollection AddSetupServices(this IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddSingleton<ApiClientService>();
            services.AddSingleton<SetupService>();
            services.AddSingleton<CommandHandler>();

            return services;
        }
    }
}
