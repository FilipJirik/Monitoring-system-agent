using Monitoring_system_agent.Services;
using Monitoring_system_client_service.Services;

namespace Monitoring_system_client_service.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMonitoringServices(this IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddSingleton<ApiClientService>();
            services.AddSingleton<LinuxMetricsService>();
            services.AddHostedService<Worker>();
            
            return services;
        }

        public static IServiceCollection AddSetupServices(this IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddSingleton<ApiClientService>();
            services.AddSingleton<SetupService>();
            
            return services;
        }
    }
}
