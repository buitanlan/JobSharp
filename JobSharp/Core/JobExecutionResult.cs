namespace JobSharp.Core;

/// <summary>
/// Represents the result of a job execution.
/// </summary>
public class JobExecutionResult
{
    /// <summary>
    /// Gets a value indicating whether the job execution was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message if the job execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the result data from the job execution.
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Gets a value indicating whether the job should be retried if it failed.
    /// </summary>
    public bool ShouldRetry { get; init; } = true;

    /// <summary>
    /// Gets the delay before the next retry attempt.
    /// </summary>
    public TimeSpan? RetryDelay { get; init; }

    /// <summary>
    /// Creates a successful job execution result.
    /// </summary>
    /// <param name="result">The result data.</param>
    /// <returns>A successful job execution result.</returns>
    public static JobExecutionResult Success(object? result = null)
    {
        return new JobExecutionResult { IsSuccess = true, Result = result };
    }

    /// <summary>
    /// Creates a failed job execution result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="shouldRetry">Whether the job should be retried.</param>
    /// <param name="retryDelay">The delay before retry.</param>
    /// <returns>A failed job execution result.</returns>
    public static JobExecutionResult Failure(string errorMessage, bool shouldRetry = true, TimeSpan? retryDelay = null)
    {
        return new JobExecutionResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ShouldRetry = shouldRetry,
            RetryDelay = retryDelay
        };
    }

    /// <summary>
    /// Creates a failed job execution result from an exception.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="shouldRetry">Whether the job should be retried.</param>
    /// <param name="retryDelay">The delay before retry.</param>
    /// <returns>A failed job execution result.</returns>
    public static JobExecutionResult Failure(Exception exception, bool shouldRetry = true, TimeSpan? retryDelay = null)
    {
        return new JobExecutionResult
        {
            IsSuccess = false,
            ErrorMessage = exception.ToString(),
            ShouldRetry = shouldRetry,
            RetryDelay = retryDelay
        };
    }
}