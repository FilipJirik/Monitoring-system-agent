namespace Monitoring_system_client_service.Results
{
    /// <summary>
    /// Represents the result of an operation.
    /// </summary>
    public class OperationResult
    {
        public bool Success { get; protected set; }
        public string? Message { get; protected set; }
        public Exception? Exception { get; protected set; }

        protected OperationResult(bool success, string? message = null, Exception? exception = null)
        {
            Success = success;
            Message = message;
            Exception = exception;
        }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static OperationResult Ok(string? message = null) 
            => new(true, message);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static OperationResult Fail(string message, Exception? exception = null)
            => new(false, message, exception);
    }

    /// <summary>
    /// Represents the result of an operation that returns data.
    /// </summary>
    public class OperationResult<T> : OperationResult
    {
        public T? Data { get; set; }

        protected OperationResult(bool success, T? data, string? message = null, Exception? exception = null)
            : base(success, message, exception)
        {
            Data = data;
        }

        /// <summary>
        /// Creates a successful result with data.
        /// </summary>
        public static OperationResult<T> Ok(T data, string? message = null)
            => new(true, data, message);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static new OperationResult<T> Fail(string message, Exception? exception = null)
            => new(false, default, message, exception);
    }
}
