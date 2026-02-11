namespace Monitoring_system_client_service.Configuration
{
    /// <summary>
    /// Configuration interface for the monitoring application.
    /// Provides abstraction over configuration sources.
    /// </summary>
    public interface IApplicationConfig
    {
        /// <summary>
        /// Gets the base URL of the monitoring server.
        /// </summary>
        string BaseUrl { get; }

        /// <summary>
        /// Gets the unique identifier of this device.
        /// </summary>
        string DeviceId { get; }

        /// <summary>
        /// Gets the API key for authenticating with the server.
        /// </summary>
        string ApiKey { get; }

        /// <summary>
        /// Gets the interval (in seconds) for metrics collection.
        /// </summary>
        int IntervalSeconds { get; }

        /// <summary>
        /// Validates that the configuration is complete and valid.
        /// </summary>
        /// <returns>True if configuration is valid, false otherwise</returns>
        bool IsValid();

        /// <summary>
        /// Gets validation error messages if configuration is invalid.
        /// </summary>
        IEnumerable<string> GetValidationErrors();
    }
}
