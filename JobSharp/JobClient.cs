using JobSharp.Core;
using JobSharp.Jobs;
using JobSharp.Storage;
using Microsoft.Extensions.Logging;

namespace JobSharp;

/// <summary>
/// Default implementation of the job client.
/// </summary>
public class JobClient : IJobClient
{
    private readonly IJobStorage _jobStorage;
    private readonly ILogger<JobClient> _logger;

    public JobClient(IJobStorage jobStorage, ILogger<JobClient> logger)
    {
        _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> EnqueueAsync<T>(T arguments, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class
    {
        var job = Job.CreateFireAndForget(arguments, maxRetryCount);
        _logger.LogDebug("Enqueueing fire-and-forget job {JobId} of type {JobType}", job.Id, typeof(T).Name);

        await _jobStorage.StoreJobAsync(job, cancellationToken);
        return job.Id;
    }

    public async Task<string> ScheduleAsync<T>(T arguments, TimeSpan delay, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class
    {
        var job = Job.CreateDelayed(arguments, delay, maxRetryCount);
        _logger.LogDebug("Scheduling delayed job {JobId} of type {JobType} for {ScheduledAt}",
            job.Id, typeof(T).Name, job.ScheduledAt);

        await _jobStorage.StoreJobAsync(job, cancellationToken);
        return job.Id;
    }

    public async Task<string> ScheduleAsync<T>(T arguments, DateTimeOffset scheduledAt, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class
    {
        var job = Job.CreateScheduled(arguments, scheduledAt, maxRetryCount);
        _logger.LogDebug("Scheduling job {JobId} of type {JobType} for {ScheduledAt}",
            job.Id, typeof(T).Name, job.ScheduledAt);

        await _jobStorage.StoreJobAsync(job, cancellationToken);
        return job.Id;
    }

    public async Task AddOrUpdateRecurringJobAsync<T>(string recurringJobId, T arguments, string cronExpression, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class
    {
        var jobTemplate = Job.CreateFireAndForget(arguments, maxRetryCount);
        jobTemplate.State = JobState.Created; // Template shouldn't be scheduled

        _logger.LogDebug("Adding/updating recurring job {RecurringJobId} of type {JobType} with cron expression {CronExpression}",
            recurringJobId, typeof(T).Name, cronExpression);

        await _jobStorage.StoreRecurringJobAsync(recurringJobId, cronExpression, jobTemplate, cancellationToken);
    }

    public async Task RemoveRecurringJobAsync(string recurringJobId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Removing recurring job {RecurringJobId}", recurringJobId);
        await _jobStorage.RemoveRecurringJobAsync(recurringJobId, cancellationToken);
    }

    public async Task<string> ContinueWithAsync<T>(string parentJobId, T arguments, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class
    {
        var continuationJob = Job.CreateContinuation(parentJobId, arguments, maxRetryCount);
        _logger.LogDebug("Creating continuation job {JobId} for parent {ParentJobId} of type {JobType}",
            continuationJob.Id, parentJobId, typeof(T).Name);

        await _jobStorage.StoreContinuationAsync(parentJobId, continuationJob, cancellationToken);
        return continuationJob.Id;
    }

    public async Task<(string BatchId, IEnumerable<string> JobIds)> EnqueueBatchAsync<T>(IEnumerable<T> argumentsList, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class
    {
        var batchId = Guid.NewGuid().ToString();
        var jobs = Job.CreateBatch(batchId, argumentsList, maxRetryCount).ToList();

        _logger.LogDebug("Creating batch {BatchId} with {JobCount} jobs of type {JobType}",
            batchId, jobs.Count, typeof(T).Name);

        await _jobStorage.StoreBatchAsync(batchId, jobs, cancellationToken);
        return (batchId, jobs.Select(j => j.Id));
    }

    public async Task<string> ContinueBatchWithAsync<T>(string batchId, T arguments, int maxRetryCount = 3, CancellationToken cancellationToken = default) where T : class
    {
        var continuationJob = Job.CreateFireAndForget(arguments, maxRetryCount);
        continuationJob.BatchId = batchId;
        continuationJob.State = JobState.AwaitingBatch;

        _logger.LogDebug("Creating batch continuation job {JobId} for batch {BatchId} of type {JobType}",
            continuationJob.Id, batchId, typeof(T).Name);

        await _jobStorage.StoreJobAsync(continuationJob, cancellationToken);
        return continuationJob.Id;
    }

    public async Task<IJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return await _jobStorage.GetJobAsync(jobId, cancellationToken);
    }

    public async Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobStorage.GetJobAsync(jobId, cancellationToken);
        if (job == null || job.State != JobState.Scheduled)
        {
            return false;
        }

        var mutableJob = job as Job ?? throw new InvalidOperationException("Job must be mutable for cancellation");
        mutableJob.State = JobState.Cancelled;

        _logger.LogDebug("Cancelling job {JobId}", jobId);
        await _jobStorage.UpdateJobAsync(mutableJob, cancellationToken);
        return true;
    }

    public async Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting job {JobId}", jobId);
        await _jobStorage.DeleteJobAsync(jobId, cancellationToken);
    }

    public async Task<int> GetJobCountAsync(JobState state, CancellationToken cancellationToken = default)
    {
        return await _jobStorage.GetJobCountAsync(state, cancellationToken);
    }
}