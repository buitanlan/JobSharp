using JobSharp.Core;

namespace JobSharp.Storage;

/// <summary>
/// Defines the contract for persistent job storage operations.
/// </summary>
public interface IJobStorage
{
    /// <summary>
    /// Stores a new job in the storage.
    /// </summary>
    /// <param name="job">The job to store.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<string> StoreJobAsync(IJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing job in the storage.
    /// </summary>
    /// <param name="job">The job to update.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateJobAsync(IJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a job by its identifier.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The job if found, otherwise null.</returns>
    Task<IJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves jobs that are ready to be executed.
    /// </summary>
    /// <param name="batchSize">The maximum number of jobs to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A collection of jobs ready for execution.</returns>
    Task<IEnumerable<IJob>> GetScheduledJobsAsync(int batchSize = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves jobs in a specific state.
    /// </summary>
    /// <param name="state">The job state to filter by.</param>
    /// <param name="batchSize">The maximum number of jobs to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A collection of jobs in the specified state.</returns>
    Task<IEnumerable<IJob>> GetJobsByStateAsync(JobState state, int batchSize = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a job from the storage.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to delete.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of jobs in a specific state.
    /// </summary>
    /// <param name="state">The job state to count.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The number of jobs in the specified state.</returns>
    Task<int> GetJobCountAsync(JobState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a batch of jobs.
    /// </summary>
    /// <param name="batchId">The unique identifier for the batch.</param>
    /// <param name="jobs">The jobs in the batch.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StoreBatchAsync(string batchId, IEnumerable<IJob> jobs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all jobs in a batch.
    /// </summary>
    /// <param name="batchId">The unique identifier of the batch.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A collection of jobs in the batch.</returns>
    Task<IEnumerable<IJob>> GetBatchJobsAsync(string batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a continuation job that will be executed after a parent job completes.
    /// </summary>
    /// <param name="parentJobId">The identifier of the parent job.</param>
    /// <param name="continuationJob">The continuation job.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StoreContinuationAsync(string parentJobId, IJob continuationJob, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets continuation jobs for a parent job.
    /// </summary>
    /// <param name="parentJobId">The identifier of the parent job.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A collection of continuation jobs.</returns>
    Task<IEnumerable<IJob>> GetContinuationsAsync(string parentJobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a recurring job schedule.
    /// </summary>
    /// <param name="recurringJobId">The unique identifier for the recurring job.</param>
    /// <param name="cronExpression">The cron expression defining the schedule.</param>
    /// <param name="jobTemplate">The job template to create instances from.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StoreRecurringJobAsync(string recurringJobId, string cronExpression, IJob jobTemplate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all recurring job schedules.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A collection of recurring job information.</returns>
    Task<IEnumerable<RecurringJobInfo>> GetRecurringJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a recurring job schedule.
    /// </summary>
    /// <param name="recurringJobId">The unique identifier of the recurring job to remove.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveRecurringJobAsync(string recurringJobId, CancellationToken cancellationToken = default);
}