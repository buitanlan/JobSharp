using JobSharp.Core;
using JobSharp.Jobs;

namespace JobSharp;

/// <summary>
/// Provides the main interface for scheduling and managing jobs.
/// </summary>
public interface IJobClient
{
    /// <summary>
    /// Schedules a fire-and-forget job for immediate execution.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="arguments">The job arguments.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The unique identifier of the scheduled job.</returns>
    Task<string> EnqueueAsync<T>(T arguments, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Schedules a delayed job for execution after a specified delay.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="arguments">The job arguments.</param>
    /// <param name="delay">The delay before the job should be executed.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The unique identifier of the scheduled job.</returns>
    Task<string> ScheduleAsync<T>(T arguments, TimeSpan delay, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Schedules a job for execution at a specific time.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="arguments">The job arguments.</param>
    /// <param name="scheduledAt">The time when the job should be executed.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The unique identifier of the scheduled job.</returns>
    Task<string> ScheduleAsync<T>(T arguments, DateTimeOffset scheduledAt, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Schedules a recurring job using a cron expression.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="recurringJobId">The unique identifier for the recurring job.</param>
    /// <param name="arguments">The job arguments.</param>
    /// <param name="cronExpression">The cron expression defining the schedule.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts per job instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddOrUpdateRecurringJobAsync<T>(string recurringJobId, T arguments, string cronExpression, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Removes a recurring job.
    /// </summary>
    /// <param name="recurringJobId">The unique identifier of the recurring job to remove.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveRecurringJobAsync(string recurringJobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a continuation job that will be executed after a parent job completes successfully.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="parentJobId">The identifier of the parent job.</param>
    /// <param name="arguments">The job arguments.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The unique identifier of the continuation job.</returns>
    Task<string> ContinueWithAsync<T>(string parentJobId, T arguments, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Schedules a batch of jobs for execution.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="argumentsList">The list of job arguments for each job in the batch.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts per job.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The unique identifier of the batch and the job identifiers.</returns>
    Task<(string BatchId, IEnumerable<string> JobIds)> EnqueueBatchAsync<T>(IEnumerable<T> argumentsList, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Schedules a continuation job that will be executed after all jobs in a batch complete successfully.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="batchId">The identifier of the batch.</param>
    /// <param name="arguments">The job arguments.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The unique identifier of the batch continuation job.</returns>
    Task<string> ContinueBatchWithAsync<T>(string batchId, T arguments, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Gets information about a job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The job information if found, otherwise null.</returns>
    Task<IJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a job if it hasn't been executed yet.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to cancel.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A value indicating whether the job was cancelled.</returns>
    Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a job from storage.
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
}