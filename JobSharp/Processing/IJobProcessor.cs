using JobSharp.Core;

namespace JobSharp.Processing;

/// <summary>
/// Defines the contract for processing jobs.
/// </summary>
public interface IJobProcessor
{
    /// <summary>
    /// Starts the job processor.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the job processor.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a single job.
    /// </summary>
    /// <param name="job">The job to process.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessJobAsync(IJob job, CancellationToken cancellationToken = default);
}