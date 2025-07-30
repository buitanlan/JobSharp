using JobSharp.Core;
using JobSharp.EntityFramework.Entities;
using JobSharp.Jobs;
using JobSharp.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobSharp.EntityFramework.Storage;

/// <summary>
/// Entity Framework implementation of IJobStorage.
/// </summary>
public class EntityFrameworkJobStorage : IJobStorage
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<EntityFrameworkJobStorage> _logger;

    public EntityFrameworkJobStorage(IServiceScopeFactory serviceScopeFactory, ILogger<EntityFrameworkJobStorage> logger)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> StoreJobAsync(IJob job, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var entity = MapToEntity(job);
        context.Jobs.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Stored job {JobId} in database", job.Id);
        return job.Id;
    }

    public async Task UpdateJobAsync(IJob job, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var entity = await context.Jobs.FindAsync([job.Id], cancellationToken);
        if (entity == null)
        {
            throw new InvalidOperationException($"Job with ID {job.Id} not found");
        }

        MapToEntity(job, entity);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated job {JobId} in database", job.Id);
    }

    public async Task<IJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var entity = await context.Jobs.FindAsync([jobId], cancellationToken);
        return entity != null ? MapFromEntity(entity) : null;
    }

    public async Task<IEnumerable<IJob>> GetScheduledJobsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var now = DateTimeOffset.UtcNow;
        var entities = await context.Jobs
            .Where(j => (int)j.State == (int)JobState.Scheduled && j.ScheduledAt <= now)
            .OrderBy(j => j.ScheduledAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return entities.Select(entity => MapFromEntity(entity));
    }

    public async Task<IEnumerable<IJob>> GetJobsByStateAsync(JobState state, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var entities = await context.Jobs
            .Where(j => j.State == state)
            .OrderBy(j => j.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return entities.Select(entity => MapFromEntity(entity));
    }

    public async Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var entity = await context.Jobs.FindAsync([jobId], cancellationToken);
        if (entity != null)
        {
            context.Jobs.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Deleted job {JobId} from database", jobId);
        }
    }

    public async Task<int> GetJobCountAsync(JobState state, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        return await context.Jobs.CountAsync(j => j.State == state, cancellationToken);
    }

    public async Task StoreBatchAsync(string batchId, IEnumerable<IJob> jobs, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var entities = jobs.Select(job => MapToEntity(job)).ToList();
        context.Jobs.AddRange(entities);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Stored batch {BatchId} with {JobCount} jobs in database", batchId, entities.Count);
    }

    public async Task<IEnumerable<IJob>> GetBatchJobsAsync(string batchId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var entities = await context.Jobs
            .Where(j => j.BatchId == batchId)
            .ToListAsync(cancellationToken);

        return entities.Select(entity => MapFromEntity(entity));
    }

    public async Task StoreContinuationAsync(string parentJobId, IJob continuationJob, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var entity = MapToEntity(continuationJob);
        entity.ParentJobId = parentJobId;

        context.Jobs.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Stored continuation job {JobId} for parent {ParentJobId} in database",
            continuationJob.Id, parentJobId);
    }

    public async Task<IEnumerable<IJob>> GetContinuationsAsync(string parentJobId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var entities = await context.Jobs
            .Where(j => j.ParentJobId == parentJobId && j.State == JobState.AwaitingContinuation)
            .ToListAsync(cancellationToken);

        return entities.Select(entity => MapFromEntity(entity));
    }

    public async Task StoreRecurringJobAsync(string recurringJobId, string cronExpression, IJob jobTemplate, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var existingEntity = await context.RecurringJobs.FindAsync([recurringJobId], cancellationToken);

        if (existingEntity != null)
        {
            // Update existing recurring job
            existingEntity.CronExpression = cronExpression;
            existingEntity.JobTypeName = jobTemplate.TypeName;
            existingEntity.JobArguments = jobTemplate.Arguments;
            existingEntity.MaxRetryCount = jobTemplate.MaxRetryCount;
        }
        else
        {
            // Create new recurring job
            var entity = new RecurringJobEntity
            {
                Id = recurringJobId,
                CronExpression = cronExpression,
                JobTypeName = jobTemplate.TypeName,
                JobArguments = jobTemplate.Arguments,
                MaxRetryCount = jobTemplate.MaxRetryCount,
                CreatedAt = DateTimeOffset.UtcNow,
                IsEnabled = true
            };

            context.RecurringJobs.Add(entity);
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Stored/updated recurring job {RecurringJobId} in database", recurringJobId);
    }

    public async Task<IEnumerable<RecurringJobInfo>> GetRecurringJobsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var entities = await context.RecurringJobs
            .Where(r => r.IsEnabled)
            .ToListAsync(cancellationToken);

        return entities.Select(entity => new RecurringJobInfo
        {
            Id = entity.Id,
            CronExpression = entity.CronExpression,
            JobTemplate = new Job
            {
                Id = Guid.NewGuid().ToString(),
                TypeName = entity.JobTypeName,
                Arguments = entity.JobArguments,
                MaxRetryCount = entity.MaxRetryCount,
                State = JobState.Created
            },
            NextExecution = entity.NextExecution,
            LastExecution = entity.LastExecution,
            IsEnabled = entity.IsEnabled,
            CreatedAt = entity.CreatedAt
        });
    }

    public async Task RemoveRecurringJobAsync(string recurringJobId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();

        var entity = await context.RecurringJobs.FindAsync([recurringJobId], cancellationToken);
        if (entity != null)
        {
            context.RecurringJobs.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Removed recurring job {RecurringJobId} from database", recurringJobId);
        }
    }

    private static JobEntity MapToEntity(IJob job, JobEntity? entity = null)
    {
        entity ??= new JobEntity { Id = job.Id, TypeName = job.TypeName };

        entity.Arguments = job.Arguments;
        entity.State = job.State;
        entity.CreatedAt = job.CreatedAt;
        entity.ScheduledAt = job.ScheduledAt;
        entity.ExecutedAt = job.ExecutedAt;
        entity.RetryCount = job.RetryCount;
        entity.MaxRetryCount = job.MaxRetryCount;
        entity.ErrorMessage = job.ErrorMessage;
        entity.Result = job.Result;

        if (job is Job jobImpl)
        {
            entity.BatchId = jobImpl.BatchId;
            entity.ParentJobId = jobImpl.ParentJobId;
        }

        return entity;
    }

    private static Job MapFromEntity(JobEntity entity)
    {
        return new Job
        {
            Id = entity.Id,
            TypeName = entity.TypeName,
            Arguments = entity.Arguments,
            State = entity.State,
            CreatedAt = entity.CreatedAt,
            ScheduledAt = entity.ScheduledAt,
            ExecutedAt = entity.ExecutedAt,
            RetryCount = entity.RetryCount,
            MaxRetryCount = entity.MaxRetryCount,
            ErrorMessage = entity.ErrorMessage,
            Result = entity.Result,
            BatchId = entity.BatchId,
            ParentJobId = entity.ParentJobId
        };
    }
}