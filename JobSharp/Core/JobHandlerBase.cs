namespace JobSharp.Core;

/// <summary>
/// Base class for job handlers that provides common functionality.
/// </summary>
/// <typeparam name="TJob">The type of job this handler can process.</typeparam>
public abstract class JobHandlerBase<TJob> : IJobHandler<TJob>, IJobHandler where TJob : class
{
    /// <summary>
    /// Gets the type of job this handler can process.
    /// </summary>
    public Type JobType => typeof(TJob);

    /// <summary>
    /// Executes the specified job asynchronously.
    /// </summary>
    /// <param name="job">The job to execute.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task<JobExecutionResult> HandleAsync(TJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the specified job asynchronously (non-generic version).
    /// </summary>
    /// <param name="job">The job to execute.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task<JobExecutionResult> HandleAsync(object job, CancellationToken cancellationToken = default)
    {
        if (job is not TJob typedJob)
        {
            return Task.FromResult(JobExecutionResult.Failure(
                $"Job is not of expected type {typeof(TJob).Name}. Actual type: {job?.GetType().Name ?? "null"}",
                shouldRetry: false
            ));
        }

        return HandleAsync(typedJob, cancellationToken);
    }
}