namespace Monitoring_system_client_service.Logging
{
    /// <summary>
    /// Console-based logger for CLI operations.
    /// </summary>
    public interface IConsoleLogger
    {
        void LogInfo(string message);
        void LogDebug(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? exception = null);
    }

    /// <summary>
    /// Default implementation of IConsoleLogger using Console.WriteLine.
    /// </summary>
    public class ConsoleLogger : IConsoleLogger
    {
        public void LogInfo(string message) 
            => Console.WriteLine($"[INFO] {message}");

        public void LogDebug(string message) 
            => Console.WriteLine($"[DEBUG] {message}");

        public void LogWarning(string message) 
            => Console.WriteLine($"[WARN] {message}");

        public void LogError(string message, Exception? exception = null)
        {
            Console.WriteLine($"[ERROR] {message}");
            if (exception != null && !string.IsNullOrEmpty(exception.StackTrace))
            {
                Console.WriteLine($"[DEBUG] Stack trace: {exception.StackTrace}");
            }
        }
    }
}
