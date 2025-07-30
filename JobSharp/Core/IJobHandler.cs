namespace JobSharp.Core;

/// <summary>
/// Defines the contract for handling and executing jobs.
/// </summary>
/// <typeparam name="TJob">The type of job this handler can process.</typeparam>
public interface IJobHandler<in TJob> where TJob : class
{
    /// <summary>
    /// Executes the specified job asynchronously.
    /// </summary>
    /// <param name="job">The job to execute.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<JobExecutionResult> HandleAsync(TJob job, CancellationToken cancellationToken = default);
}

/// <summary>
/// Non-generic interface for job handlers to enable runtime dispatch.
/// </summary>
public interface IJobHandler
{
    /// <summary>
    /// Gets the type of job this handler can process.
    /// </summary>
    Type JobType { get; }

    /// <summary>
    /// Executes the specified job asynchronously.
    /// </summary>
    /// <param name="job">The job to execute.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<JobExecutionResult> HandleAsync(object job, CancellationToken cancellationToken = default);
}