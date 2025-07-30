using JobSharp.Core;
using JobSharp.Jobs;
using JobSharp.MongoDb.Models;
using JobSharp.Storage;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace JobSharp.MongoDb.Storage;

/// <summary>
/// MongoDB implementation of IJobStorage.
/// </summary>
public class MongoDbJobStorage : IJobStorage
{
    private readonly IMongoCollection<JobDocument> _jobsCollection;
    private readonly IMongoCollection<RecurringJobDocument> _recurringJobsCollection;
    private readonly ILogger<MongoDbJobStorage> _logger;

    public MongoDbJobStorage(IMongoDatabase database, ILogger<MongoDbJobStorage> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (database == null)
            throw new ArgumentNullException(nameof(database));

        _jobsCollection = database.GetCollection<JobDocument>("jobs");
        _recurringJobsCollection = database.GetCollection<RecurringJobDocument>("recurringJobs");

        // Create indexes
        CreateIndexes();
    }

    public async Task<string> StoreJobAsync(IJob job, CancellationToken cancellationToken = default)
    {
        var document = MapToDocument(job);
        await _jobsCollection.InsertOneAsync(document, cancellationToken: cancellationToken);

        _logger.LogDebug("Stored job {JobId} in MongoDB", job.Id);
        return job.Id;
    }

    public async Task UpdateJobAsync(IJob job, CancellationToken cancellationToken = default)
    {
        var document = MapToDocument(job);
        var result = await _jobsCollection.ReplaceOneAsync(
            j => j.Id == job.Id,
            document,
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            throw new InvalidOperationException($"Job with ID {job.Id} not found");
        }

        _logger.LogDebug("Updated job {JobId} in MongoDB", job.Id);
    }

    public async Task<IJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var document = await _jobsCollection.Find(j => j.Id == jobId)
            .FirstOrDefaultAsync(cancellationToken);

        return document != null ? MapFromDocument(document) : null;
    }

    public async Task<IEnumerable<IJob>> GetScheduledJobsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var documents = await _jobsCollection
            .Find(j => j.State == JobState.Scheduled && j.ScheduledAt <= now)
            .Sort(Builders<JobDocument>.Sort.Ascending(j => j.ScheduledAt))
            .Limit(batchSize)
            .ToListAsync(cancellationToken);

        return documents.Select(MapFromDocument);
    }

    public async Task<IEnumerable<IJob>> GetJobsByStateAsync(JobState state, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var documents = await _jobsCollection
            .Find(j => j.State == state)
            .Sort(Builders<JobDocument>.Sort.Ascending(j => j.CreatedAt))
            .Limit(batchSize)
            .ToListAsync(cancellationToken);

        return documents.Select(MapFromDocument);
    }

    public async Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var result = await _jobsCollection.DeleteOneAsync(j => j.Id == jobId, cancellationToken);

        if (result.DeletedCount > 0)
        {
            _logger.LogDebug("Deleted job {JobId} from MongoDB", jobId);
        }
    }

    public async Task<int> GetJobCountAsync(JobState state, CancellationToken cancellationToken = default)
    {
        var count = await _jobsCollection.CountDocumentsAsync(j => j.State == state, cancellationToken: cancellationToken);
        return (int)count;
    }

    public async Task StoreBatchAsync(string batchId, IEnumerable<IJob> jobs, CancellationToken cancellationToken = default)
    {
        var documents = jobs.Select(MapToDocument).ToList();
        await _jobsCollection.InsertManyAsync(documents, cancellationToken: cancellationToken);

        _logger.LogDebug("Stored batch {BatchId} with {JobCount} jobs in MongoDB", batchId, documents.Count);
    }

    public async Task<IEnumerable<IJob>> GetBatchJobsAsync(string batchId, CancellationToken cancellationToken = default)
    {
        var documents = await _jobsCollection
            .Find(j => j.BatchId == batchId)
            .ToListAsync(cancellationToken);

        return documents.Select(MapFromDocument);
    }

    public async Task StoreContinuationAsync(string parentJobId, IJob continuationJob, CancellationToken cancellationToken = default)
    {
        var document = MapToDocument(continuationJob);
        document.ParentJobId = parentJobId;

        await _jobsCollection.InsertOneAsync(document, cancellationToken: cancellationToken);

        _logger.LogDebug("Stored continuation job {JobId} for parent {ParentJobId} in MongoDB",
            continuationJob.Id, parentJobId);
    }

    public async Task<IEnumerable<IJob>> GetContinuationsAsync(string parentJobId, CancellationToken cancellationToken = default)
    {
        var documents = await _jobsCollection
            .Find(j => j.ParentJobId == parentJobId && j.State == JobState.AwaitingContinuation)
            .ToListAsync(cancellationToken);

        return documents.Select(MapFromDocument);
    }

    public async Task StoreRecurringJobAsync(string recurringJobId, string cronExpression, IJob jobTemplate, CancellationToken cancellationToken = default)
    {
        var document = new RecurringJobDocument
        {
            Id = recurringJobId,
            CronExpression = cronExpression,
            JobTypeName = jobTemplate.TypeName,
            JobArguments = jobTemplate.Arguments,
            MaxRetryCount = jobTemplate.MaxRetryCount,
            CreatedAt = DateTime.UtcNow,
            IsEnabled = true
        };

        await _recurringJobsCollection.ReplaceOneAsync(
            r => r.Id == recurringJobId,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        _logger.LogDebug("Stored/updated recurring job {RecurringJobId} in MongoDB", recurringJobId);
    }

    public async Task<IEnumerable<RecurringJobInfo>> GetRecurringJobsAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _recurringJobsCollection
            .Find(r => r.IsEnabled)
            .ToListAsync(cancellationToken);

        return documents.Select(document => new RecurringJobInfo
        {
            Id = document.Id,
            CronExpression = document.CronExpression,
            JobTemplate = new Job
            {
                Id = Guid.NewGuid().ToString(),
                TypeName = document.JobTypeName,
                Arguments = document.JobArguments,
                MaxRetryCount = document.MaxRetryCount,
                State = JobState.Created
            },
            NextExecution = document.NextExecution?.ToUniversalTime(),
            LastExecution = document.LastExecution?.ToUniversalTime(),
            IsEnabled = document.IsEnabled,
            CreatedAt = document.CreatedAt.ToUniversalTime()
        });
    }

    public async Task RemoveRecurringJobAsync(string recurringJobId, CancellationToken cancellationToken = default)
    {
        var result = await _recurringJobsCollection.DeleteOneAsync(r => r.Id == recurringJobId, cancellationToken);

        if (result.DeletedCount > 0)
        {
            _logger.LogDebug("Removed recurring job {RecurringJobId} from MongoDB", recurringJobId);
        }
    }

    private void CreateIndexes()
    {
        // Job indexes
        var jobIndexes = new[]
        {
            new CreateIndexModel<JobDocument>(Builders<JobDocument>.IndexKeys.Ascending(j => j.State)),
            new CreateIndexModel<JobDocument>(Builders<JobDocument>.IndexKeys.Ascending(j => j.ScheduledAt)),
            new CreateIndexModel<JobDocument>(Builders<JobDocument>.IndexKeys.Ascending(j => j.BatchId)),
            new CreateIndexModel<JobDocument>(Builders<JobDocument>.IndexKeys.Ascending(j => j.ParentJobId)),
            new CreateIndexModel<JobDocument>(Builders<JobDocument>.IndexKeys
                .Ascending(j => j.State)
                .Ascending(j => j.ScheduledAt))
        };

        _jobsCollection.Indexes.CreateMany(jobIndexes);

        // Recurring job indexes
        var recurringJobIndexes = new[]
        {
            new CreateIndexModel<RecurringJobDocument>(Builders<RecurringJobDocument>.IndexKeys.Ascending(r => r.IsEnabled)),
            new CreateIndexModel<RecurringJobDocument>(Builders<RecurringJobDocument>.IndexKeys.Ascending(r => r.NextExecution))
        };

        _recurringJobsCollection.Indexes.CreateMany(recurringJobIndexes);
    }

    private static JobDocument MapToDocument(IJob job)
    {
        var document = new JobDocument
        {
            Id = job.Id,
            TypeName = job.TypeName,
            Arguments = job.Arguments,
            State = job.State,
            CreatedAt = job.CreatedAt.UtcDateTime,
            ScheduledAt = job.ScheduledAt?.UtcDateTime,
            ExecutedAt = job.ExecutedAt?.UtcDateTime,
            RetryCount = job.RetryCount,
            MaxRetryCount = job.MaxRetryCount,
            ErrorMessage = job.ErrorMessage,
            Result = job.Result
        };

        if (job is Job jobImpl)
        {
            document.BatchId = jobImpl.BatchId;
            document.ParentJobId = jobImpl.ParentJobId;
        }

        return document;
    }

    private static Job MapFromDocument(JobDocument document)
    {
        return new Job
        {
            Id = document.Id,
            TypeName = document.TypeName,
            Arguments = document.Arguments,
            State = document.State,
            CreatedAt = new DateTimeOffset(document.CreatedAt, TimeSpan.Zero),
            ScheduledAt = document.ScheduledAt.HasValue ? new DateTimeOffset(document.ScheduledAt.Value, TimeSpan.Zero) : null,
            ExecutedAt = document.ExecutedAt.HasValue ? new DateTimeOffset(document.ExecutedAt.Value, TimeSpan.Zero) : null,
            RetryCount = document.RetryCount,
            MaxRetryCount = document.MaxRetryCount,
            ErrorMessage = document.ErrorMessage,
            Result = document.Result,
            BatchId = document.BatchId,
            ParentJobId = document.ParentJobId
        };
    }
}