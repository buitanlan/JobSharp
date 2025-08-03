using JobSharp.Core;
using JobSharp.Jobs;
using JobSharp.Redis.Models;
using JobSharp.Storage;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace JobSharp.Redis.Storage;

/// <summary>
/// Redis implementation of IJobStorage.
/// </summary>
public class RedisJobStorage : IJobStorage
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisJobStorage> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string JobsKeyPrefix = "jobsharp:job:";
    private const string JobsByStateKeyPrefix = "jobsharp:job:state:";
    private const string ScheduledJobsKey = "jobsharp:jobs:scheduled";
    private const string BatchJobsKeyPrefix = "jobsharp:jobs:batch:";
    private const string ContinuationJobsKeyPrefix = "jobsharp:jobs:continuation:";
    private const string RecurringJobsKey = "jobsharp:recurringjob:";
    private const string RecurringJobsSetKey = "jobsharp:recurring:all";

    public RedisJobStorage(IDatabase database, ILogger<RedisJobStorage> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<string> StoreJobAsync(IJob job, CancellationToken cancellationToken = default)
    {
        var jobData = MapToJobData(job);
        var json = JsonSerializer.Serialize(jobData, _jsonOptions);
        var jobKey = GetJobKey(job.Id);

        await _database.StringSetAsync(jobKey, json);

        // Add to state-specific set
        await _database.SetAddAsync(GetJobStateKey(job.State), job.Id);

        // Add to scheduled set if scheduled
        if (job.State == JobState.Scheduled && job.ScheduledAt.HasValue)
        {
            var score = job.ScheduledAt.Value.ToUnixTimeSeconds();
            await _database.SortedSetAddAsync(ScheduledJobsKey, job.Id, score);
        }

        // Add to batch set if part of batch
        if (jobData.BatchId != null)
        {
            await _database.SetAddAsync(GetBatchKey(jobData.BatchId), job.Id);
        }

        // Add to continuation set if has parent
        if (jobData.ParentJobId != null)
        {
            await _database.SetAddAsync(GetContinuationKey(jobData.ParentJobId), job.Id);
        }

        _logger.LogDebug("Stored job {JobId} in Redis", job.Id);
        return job.Id;
    }

    public async Task UpdateJobAsync(IJob job, CancellationToken cancellationToken = default)
    {
        var jobKey = GetJobKey(job.Id);
        var existingJson = await _database.StringGetAsync(jobKey);

        if (!existingJson.HasValue)
        {
            throw new InvalidOperationException($"Job with ID {job.Id} not found");
        }

        var existingJobData = JsonSerializer.Deserialize<JobData>(existingJson!, _jsonOptions)!;
        var newJobData = MapToJobData(job);

        // Remove from old state set
        await _database.SetRemoveAsync(GetJobStateKey(existingJobData.State), job.Id);

        // Remove from scheduled set if it was scheduled
        if (existingJobData.State == JobState.Scheduled)
        {
            await _database.SortedSetRemoveAsync(ScheduledJobsKey, job.Id);
        }

        // Update job data
        var json = JsonSerializer.Serialize(newJobData, _jsonOptions);
        await _database.StringSetAsync(jobKey, json);

        // Add to new state set
        await _database.SetAddAsync(GetJobStateKey(job.State), job.Id);

        // Add to scheduled set if now scheduled
        if (job.State == JobState.Scheduled && job.ScheduledAt.HasValue)
        {
            var score = job.ScheduledAt.Value.ToUnixTimeSeconds();
            await _database.SortedSetAddAsync(ScheduledJobsKey, job.Id, score);
        }

        _logger.LogDebug("Updated job {JobId} in Redis", job.Id);
    }

    public async Task<IJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var jobKey = GetJobKey(jobId);
        var json = await _database.StringGetAsync(jobKey);

        if (!json.HasValue)
            return null;

        var jobData = JsonSerializer.Deserialize<JobData>(json!, _jsonOptions)!;
        return MapFromJobData(jobData);
    }

    public async Task<IEnumerable<IJob>> GetScheduledJobsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting scheduled jobs with batch size {BatchSize}", batchSize);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var jobIds = await _database.SortedSetRangeByScoreAsync(ScheduledJobsKey, 0, now, take: batchSize);
        _logger.LogDebug("Found {JobIdCount} scheduled job IDs", jobIds.Length);

        var jobs = new List<IJob>();
        foreach (var jobId in jobIds)
        {
            var job = await GetJobAsync(jobId!, cancellationToken);
            if (job != null)
                jobs.Add(job);
        }

        return jobs;
    }

    public async Task<IEnumerable<IJob>> GetJobsByStateAsync(JobState state, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var stateKey = GetJobStateKey(state);
        var jobIds = await _database.SetRandomMembersAsync(stateKey, batchSize);

        var jobs = new List<IJob>();
        foreach (var jobId in jobIds)
        {
            var job = await GetJobAsync(jobId!, cancellationToken);
            if (job != null)
                jobs.Add(job);
        }

        return jobs;
    }

    public async Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var job = await GetJobAsync(jobId, cancellationToken);
        if (job == null)
            return;

        var jobKey = GetJobKey(jobId);

        // Remove from all sets
        await _database.SetRemoveAsync(GetJobStateKey(job.State), jobId);
        await _database.SortedSetRemoveAsync(ScheduledJobsKey, jobId);

        if (job is Job jobImpl)
        {
            if (jobImpl.BatchId != null)
                await _database.SetRemoveAsync(GetBatchKey(jobImpl.BatchId), jobId);

            if (jobImpl.ParentJobId != null)
                await _database.SetRemoveAsync(GetContinuationKey(jobImpl.ParentJobId), jobId);
        }

        // Delete the job data
        await _database.KeyDeleteAsync(jobKey);

        _logger.LogDebug("Deleted job {JobId} from Redis", jobId);
    }

    public async Task<int> GetJobCountAsync(JobState state, CancellationToken cancellationToken = default)
    {
        var stateKey = GetJobStateKey(state);
        var count = await _database.SetLengthAsync(stateKey);
        return (int)count;
    }

    public async Task StoreBatchAsync(string batchId, IEnumerable<IJob> jobs, CancellationToken cancellationToken = default)
    {
        var jobList = jobs.ToList();
        var tasks = jobList.Select(job => StoreJobAsync(job, cancellationToken));
        await Task.WhenAll(tasks);

        _logger.LogDebug("Stored batch {BatchId} with {JobCount} jobs in Redis", batchId, jobList.Count);
    }

    public async Task<IEnumerable<IJob>> GetBatchJobsAsync(string batchId, CancellationToken cancellationToken = default)
    {
        var batchKey = GetBatchKey(batchId);
        var jobIds = await _database.SetMembersAsync(batchKey);

        var jobs = new List<IJob>();
        foreach (var jobId in jobIds)
        {
            var job = await GetJobAsync(jobId!, cancellationToken);
            if (job != null)
                jobs.Add(job);
        }

        return jobs;
    }

    public async Task StoreContinuationAsync(string parentJobId, IJob continuationJob, CancellationToken cancellationToken = default)
    {
        // Set the parent job ID on the continuation job
        if (continuationJob is Job jobImpl)
        {
            jobImpl.ParentJobId = parentJobId;
        }

        await StoreJobAsync(continuationJob, cancellationToken);

        _logger.LogDebug("Stored continuation job {JobId} for parent {ParentJobId} in Redis",
            continuationJob.Id, parentJobId);
    }

    public async Task<IEnumerable<IJob>> GetContinuationsAsync(string parentJobId, CancellationToken cancellationToken = default)
    {
        var continuationKey = GetContinuationKey(parentJobId);
        var jobIds = await _database.SetMembersAsync(continuationKey);

        var jobs = new List<IJob>();
        foreach (var jobId in jobIds)
        {
            var job = await GetJobAsync(jobId!, cancellationToken);
            if (job != null && job.State == JobState.AwaitingContinuation)
                jobs.Add(job);
        }

        return jobs;
    }

    public async Task StoreRecurringJobAsync(string recurringJobId, string cronExpression, IJob jobTemplate, CancellationToken cancellationToken = default)
    {
        var recurringJobData = new RecurringJobData
        {
            Id = recurringJobId,
            CronExpression = cronExpression,
            JobTypeName = jobTemplate.TypeName,
            JobArguments = jobTemplate.Arguments,
            MaxRetryCount = jobTemplate.MaxRetryCount,
            CreatedAt = DateTimeOffset.UtcNow,
            IsEnabled = true
        };

        var json = JsonSerializer.Serialize(recurringJobData, _jsonOptions);
        var recurringJobKey = GetRecurringJobKey(recurringJobId);

        await _database.StringSetAsync(recurringJobKey, json);
        await _database.SetAddAsync(RecurringJobsSetKey, recurringJobId);

        _logger.LogDebug("Stored/updated recurring job {RecurringJobId} in Redis", recurringJobId);
    }

    public async Task<IEnumerable<RecurringJobInfo>> GetRecurringJobsAsync(CancellationToken cancellationToken = default)
    {
        var recurringJobIds = await _database.SetMembersAsync(RecurringJobsSetKey);
        var recurringJobs = new List<RecurringJobInfo>();

        foreach (var jobId in recurringJobIds)
        {
            var recurringJobKey = GetRecurringJobKey(jobId!);
            var json = await _database.StringGetAsync(recurringJobKey);

            if (json.HasValue)
            {
                var recurringJobData = JsonSerializer.Deserialize<RecurringJobData>(json!, _jsonOptions)!;

                if (recurringJobData.IsEnabled)
                {
                    recurringJobs.Add(new RecurringJobInfo
                    {
                        Id = recurringJobData.Id,
                        CronExpression = recurringJobData.CronExpression,
                        JobTemplate = new Job
                        {
                            Id = Guid.NewGuid().ToString(),
                            TypeName = recurringJobData.JobTypeName,
                            Arguments = recurringJobData.JobArguments,
                            MaxRetryCount = recurringJobData.MaxRetryCount,
                            State = JobState.Created
                        },
                        NextExecution = recurringJobData.NextExecution,
                        LastExecution = recurringJobData.LastExecution,
                        IsEnabled = recurringJobData.IsEnabled,
                        CreatedAt = recurringJobData.CreatedAt
                    });
                }
            }
        }

        return recurringJobs;
    }

    public async Task RemoveRecurringJobAsync(string recurringJobId, CancellationToken cancellationToken = default)
    {
        var recurringJobKey = GetRecurringJobKey(recurringJobId);

        await _database.KeyDeleteAsync(recurringJobKey);
        await _database.SetRemoveAsync(RecurringJobsSetKey, recurringJobId);

        _logger.LogDebug("Removed recurring job {RecurringJobId} from Redis", recurringJobId);
    }

    private static string GetJobKey(string jobId) => $"{JobsKeyPrefix}{jobId}";
    private static string GetJobStateKey(JobState state) => $"{JobsByStateKeyPrefix}{(int)state}";
    private static string GetBatchKey(string batchId) => $"{BatchJobsKeyPrefix}{batchId}";
    private static string GetContinuationKey(string parentJobId) => $"{ContinuationJobsKeyPrefix}{parentJobId}";
    private static string GetRecurringJobKey(string recurringJobId) => $"{RecurringJobsKey}{recurringJobId}";

    private static JobData MapToJobData(IJob job)
    {
        var jobData = new JobData
        {
            Id = job.Id,
            TypeName = job.TypeName,
            Arguments = job.Arguments,
            State = job.State,
            CreatedAt = job.CreatedAt,
            ScheduledAt = job.ScheduledAt,
            ExecutedAt = job.ExecutedAt,
            RetryCount = job.RetryCount,
            MaxRetryCount = job.MaxRetryCount,
            ErrorMessage = job.ErrorMessage,
            Result = job.Result
        };

        if (job is Job jobImpl)
        {
            jobData.BatchId = jobImpl.BatchId;
            jobData.ParentJobId = jobImpl.ParentJobId;
        }

        return jobData;
    }

    private static Job MapFromJobData(JobData jobData)
    {
        return new Job
        {
            Id = jobData.Id,
            TypeName = jobData.TypeName,
            Arguments = jobData.Arguments,
            State = jobData.State,
            CreatedAt = jobData.CreatedAt,
            ScheduledAt = jobData.ScheduledAt,
            ExecutedAt = jobData.ExecutedAt,
            RetryCount = jobData.RetryCount,
            MaxRetryCount = jobData.MaxRetryCount,
            ErrorMessage = jobData.ErrorMessage,
            Result = jobData.Result,
            BatchId = jobData.BatchId,
            ParentJobId = jobData.ParentJobId
        };
    }
}