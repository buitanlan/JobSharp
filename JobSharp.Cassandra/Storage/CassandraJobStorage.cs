using Cassandra;
using Cassandra.Mapping;
using JobSharp.Cassandra.Models;
using JobSharp.Core;
using JobSharp.Jobs;
using JobSharp.Storage;
using Microsoft.Extensions.Logging;

namespace JobSharp.Cassandra.Storage;

/// <summary>
/// Cassandra implementation of IJobStorage.
/// </summary>
public class CassandraJobStorage : IJobStorage
{
    private readonly ISession _session;
    private readonly IMapper _mapper;
    private readonly ILogger<CassandraJobStorage> _logger;
    private const int ScheduledJobsBuckets = 10; // Number of buckets for scheduled jobs partitioning

    public CassandraJobStorage(ISession session, IMapper mapper, ILogger<CassandraJobStorage> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> StoreJobAsync(IJob job, CancellationToken cancellationToken = default)
    {
        var jobRow = MapToJobRow(job);

        // Insert into main jobs table
        await _mapper.InsertAsync(jobRow);

        // Insert into jobs_by_state table for efficient state queries
        var jobsByStateRow = new JobsByStateRow
        {
            State = (int)job.State,
            CreatedAt = job.CreatedAt,
            JobId = job.Id
        };
        await _mapper.InsertAsync(jobsByStateRow);

        // Insert into scheduled_jobs table if scheduled
        if (job.State == JobState.Scheduled && job.ScheduledAt.HasValue)
        {
            var scheduledJobRow = new ScheduledJobsRow
            {
                Bucket = GetScheduledJobBucket(job.ScheduledAt.Value),
                ScheduledAt = job.ScheduledAt.Value,
                JobId = job.Id
            };
            await _mapper.InsertAsync(scheduledJobRow);
        }

        _logger.LogDebug("Stored job {JobId} in Cassandra", job.Id);
        return job.Id;
    }

    public async Task UpdateJobAsync(IJob job, CancellationToken cancellationToken = default)
    {
        var existingJobRow = await _mapper.FirstOrDefaultAsync<JobRow>("WHERE id = ?", job.Id);
        if (existingJobRow == null)
        {
            throw new InvalidOperationException($"Job with ID {job.Id} not found");
        }

        var oldState = (JobState)existingJobRow.State;
        var newJobRow = MapToJobRow(job);

        // Update main jobs table
        await _mapper.UpdateAsync(newJobRow);

        // Handle state change
        if (oldState != job.State)
        {
            // Remove from old state
            await _mapper.DeleteAsync<JobsByStateRow>("WHERE state = ? AND created_at = ? AND job_id = ?",
                (int)oldState, existingJobRow.CreatedAt, job.Id);

            // Add to new state
            var newJobsByStateRow = new JobsByStateRow
            {
                State = (int)job.State,
                CreatedAt = job.CreatedAt,
                JobId = job.Id
            };
            await _mapper.InsertAsync(newJobsByStateRow);
        }

        // Handle scheduled state changes
        if (oldState == JobState.Scheduled && job.State != JobState.Scheduled)
        {
            // Remove from scheduled_jobs table
            var oldBucket = GetScheduledJobBucket(existingJobRow.ScheduledAt ?? DateTimeOffset.UtcNow);
            await _mapper.DeleteAsync<ScheduledJobsRow>("WHERE bucket = ? AND scheduled_at = ? AND job_id = ?",
                oldBucket, existingJobRow.ScheduledAt, job.Id);
        }
        else if (job.State == JobState.Scheduled && job.ScheduledAt.HasValue)
        {
            // Add to scheduled_jobs table
            var scheduledJobRow = new ScheduledJobsRow
            {
                Bucket = GetScheduledJobBucket(job.ScheduledAt.Value),
                ScheduledAt = job.ScheduledAt.Value,
                JobId = job.Id
            };
            await _mapper.InsertAsync(scheduledJobRow);
        }

        _logger.LogDebug("Updated job {JobId} in Cassandra", job.Id);
    }

    public async Task<IJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var jobRow = await _mapper.FirstOrDefaultAsync<JobRow>("WHERE id = ?", jobId);
        return jobRow != null ? MapFromJobRow(jobRow) : null;
    }

    public async Task<IEnumerable<IJob>> GetScheduledJobsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var jobs = new List<IJob>();

        // Query multiple buckets to get scheduled jobs
        for (int bucket = 0; bucket < ScheduledJobsBuckets && jobs.Count < batchSize; bucket++)
        {
            var scheduledJobRows = await _mapper.FetchAsync<ScheduledJobsRow>(
                "WHERE bucket = ? AND scheduled_at <= ?", bucket, now);

            foreach (var scheduledJobRow in scheduledJobRows.Take(batchSize - jobs.Count))
            {
                var job = await GetJobAsync(scheduledJobRow.JobId, cancellationToken);
                if (job != null && job.State == JobState.Scheduled)
                {
                    jobs.Add(job);
                }
            }
        }

        return jobs.OrderBy(j => j.ScheduledAt).Take(batchSize);
    }

    public async Task<IEnumerable<IJob>> GetJobsByStateAsync(JobState state, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var jobsByStateRows = await _mapper.FetchAsync<JobsByStateRow>(
            "WHERE state = ? LIMIT ?", (int)state, batchSize);

        var jobs = new List<IJob>();
        foreach (var jobsByStateRow in jobsByStateRows)
        {
            var job = await GetJobAsync(jobsByStateRow.JobId, cancellationToken);
            if (job != null)
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }

    public async Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var job = await GetJobAsync(jobId, cancellationToken);
        if (job == null)
            return;

        // Delete from main table
        await _mapper.DeleteAsync<JobRow>("WHERE id = ?", jobId);

        // Delete from jobs_by_state table
        await _mapper.DeleteAsync<JobsByStateRow>("WHERE state = ? AND created_at = ? AND job_id = ?",
            (int)job.State, job.CreatedAt, jobId);

        // Delete from scheduled_jobs table if scheduled
        if (job.State == JobState.Scheduled && job.ScheduledAt.HasValue)
        {
            var bucket = GetScheduledJobBucket(job.ScheduledAt.Value);
            await _mapper.DeleteAsync<ScheduledJobsRow>("WHERE bucket = ? AND scheduled_at = ? AND job_id = ?",
                bucket, job.ScheduledAt, jobId);
        }

        _logger.LogDebug("Deleted job {JobId} from Cassandra", jobId);
    }

    public async Task<int> GetJobCountAsync(JobState state, CancellationToken cancellationToken = default)
    {
        var result = await _session.ExecuteAsync(new SimpleStatement(
            "SELECT COUNT(*) FROM jobs_by_state WHERE state = ?", (int)state));

        return (int)result.First().GetValue<long>(0);
    }

    public async Task StoreBatchAsync(string batchId, IEnumerable<IJob> jobs, CancellationToken cancellationToken = default)
    {
        var jobList = jobs.ToList();
        var tasks = jobList.Select(job => StoreJobAsync(job, cancellationToken));
        await Task.WhenAll(tasks);

        _logger.LogDebug("Stored batch {BatchId} with {JobCount} jobs in Cassandra", batchId, jobList.Count);
    }

    public async Task<IEnumerable<IJob>> GetBatchJobsAsync(string batchId, CancellationToken cancellationToken = default)
    {
        var jobRows = await _mapper.FetchAsync<JobRow>("WHERE batch_id = ? ALLOW FILTERING", batchId);
        return jobRows.Select(MapFromJobRow);
    }

    public async Task StoreContinuationAsync(string parentJobId, IJob continuationJob, CancellationToken cancellationToken = default)
    {
        // Set the parent job ID on the continuation job
        if (continuationJob is Job jobImpl)
        {
            jobImpl.ParentJobId = parentJobId;
        }

        await StoreJobAsync(continuationJob, cancellationToken);

        _logger.LogDebug("Stored continuation job {JobId} for parent {ParentJobId} in Cassandra",
            continuationJob.Id, parentJobId);
    }

    public async Task<IEnumerable<IJob>> GetContinuationsAsync(string parentJobId, CancellationToken cancellationToken = default)
    {
        var jobRows = await _mapper.FetchAsync<JobRow>(
            "WHERE parent_job_id = ? AND state = ? ALLOW FILTERING",
            parentJobId, (int)JobState.AwaitingContinuation);

        return jobRows.Select(MapFromJobRow);
    }

    public async Task StoreRecurringJobAsync(string recurringJobId, string cronExpression, IJob jobTemplate, CancellationToken cancellationToken = default)
    {
        var recurringJobRow = new RecurringJobRow
        {
            Id = recurringJobId,
            CronExpression = cronExpression,
            JobTypeName = jobTemplate.TypeName,
            JobArguments = jobTemplate.Arguments,
            MaxRetryCount = jobTemplate.MaxRetryCount,
            CreatedAt = DateTimeOffset.UtcNow,
            IsEnabled = true
        };

        await _mapper.InsertAsync(recurringJobRow);
        _logger.LogDebug("Stored/updated recurring job {RecurringJobId} in Cassandra", recurringJobId);
    }

    public async Task<IEnumerable<RecurringJobInfo>> GetRecurringJobsAsync(CancellationToken cancellationToken = default)
    {
        var recurringJobRows = await _mapper.FetchAsync<RecurringJobRow>("WHERE is_enabled = ? ALLOW FILTERING", true);

        return recurringJobRows.Select(row => new RecurringJobInfo
        {
            Id = row.Id,
            CronExpression = row.CronExpression,
            JobTemplate = new Job
            {
                Id = Guid.NewGuid().ToString(),
                TypeName = row.JobTypeName,
                Arguments = row.JobArguments,
                MaxRetryCount = row.MaxRetryCount,
                State = JobState.Created
            },
            NextExecution = row.NextExecution,
            LastExecution = row.LastExecution,
            IsEnabled = row.IsEnabled,
            CreatedAt = row.CreatedAt
        });
    }

    public async Task RemoveRecurringJobAsync(string recurringJobId, CancellationToken cancellationToken = default)
    {
        await _mapper.DeleteAsync<RecurringJobRow>("WHERE id = ?", recurringJobId);
        _logger.LogDebug("Removed recurring job {RecurringJobId} from Cassandra", recurringJobId);
    }

    private static int GetScheduledJobBucket(DateTimeOffset scheduledAt)
    {
        // Distribute scheduled jobs across buckets based on scheduled time
        return Math.Abs(scheduledAt.GetHashCode()) % ScheduledJobsBuckets;
    }

    private static JobRow MapToJobRow(IJob job)
    {
        var jobRow = new JobRow
        {
            Id = job.Id,
            TypeName = job.TypeName,
            Arguments = job.Arguments,
            State = (int)job.State,
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
            jobRow.BatchId = jobImpl.BatchId;
            jobRow.ParentJobId = jobImpl.ParentJobId;
        }

        return jobRow;
    }

    private static Job MapFromJobRow(JobRow jobRow)
    {
        return new Job
        {
            Id = jobRow.Id,
            TypeName = jobRow.TypeName,
            Arguments = jobRow.Arguments,
            State = (JobState)jobRow.State,
            CreatedAt = jobRow.CreatedAt,
            ScheduledAt = jobRow.ScheduledAt,
            ExecutedAt = jobRow.ExecutedAt,
            RetryCount = jobRow.RetryCount,
            MaxRetryCount = jobRow.MaxRetryCount,
            ErrorMessage = jobRow.ErrorMessage,
            Result = jobRow.Result,
            BatchId = jobRow.BatchId,
            ParentJobId = jobRow.ParentJobId
        };
    }
}