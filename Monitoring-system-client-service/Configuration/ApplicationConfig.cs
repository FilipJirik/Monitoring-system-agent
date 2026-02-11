using Monitoring_system_agent.Models;

namespace Monitoring_system_client_service.Configuration
{
    /// <summary>
    /// Implementation of IApplicationConfig using ConfigModel.
    /// </summary>
    public class ApplicationConfig : IApplicationConfig
    {
        private readonly ConfigModel _config;

        public ApplicationConfig(ConfigModel config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public string BaseUrl => _config.BaseUrl;
        public string DeviceId => _config.DeviceId;
        public string ApiKey => _config.ApiKey;
        public int IntervalSeconds => _config.IntervalSeconds;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(BaseUrl) &&
                   !string.IsNullOrWhiteSpace(DeviceId) &&
                   !string.IsNullOrWhiteSpace(ApiKey) &&
                   IntervalSeconds > 0 &&
                   Guid.TryParse(DeviceId, out _);
        }

        public IEnumerable<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(BaseUrl))
                errors.Add("BaseUrl is required");

            if (string.IsNullOrWhiteSpace(DeviceId))
                errors.Add("DeviceId is required");
            else if (!Guid.TryParse(DeviceId, out _))
                errors.Add("DeviceId must be a valid GUID");

            if (string.IsNullOrWhiteSpace(ApiKey))
                errors.Add("ApiKey is required");

            if (IntervalSeconds <= 0)
                errors.Add("IntervalSeconds must be greater than 0");

            return errors;
        }
    }
}
