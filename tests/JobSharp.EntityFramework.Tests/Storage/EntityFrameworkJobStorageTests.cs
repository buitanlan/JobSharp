using JobSharp.Core;
using JobSharp.EntityFramework;
using JobSharp.EntityFramework.Storage;
using JobSharp.Jobs;
using JobSharp.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace JobSharp.EntityFramework.Tests.Storage;

public class EntityFrameworkJobStorageTests : IDisposable
{
    private readonly JobSharpDbContext _context;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<EntityFrameworkJobStorage> _logger;
    private readonly EntityFrameworkJobStorage _storage;

    public EntityFrameworkJobStorageTests()
    {
        var options = new DbContextOptionsBuilder<JobSharpDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new JobSharpDbContext(options);
        _logger = Substitute.For<ILogger<EntityFrameworkJobStorage>>();

        var serviceScope = Substitute.For<IServiceScope>();
        serviceScope.ServiceProvider.GetRequiredService<JobSharpDbContext>().Returns(_context);

        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScopeFactory.CreateScope().Returns(serviceScope);

        _storage = new EntityFrameworkJobStorage(_serviceScopeFactory, _logger);
    }

    [Fact]
    public async Task StoreJobAsync_ShouldStoreJobInDatabase()
    {
        // Arrange
        var job = new Job
        {
            Id = "test-job-1",
            TypeName = "TestJob",
            Arguments = "{\"value\": 42}",
            State = JobState.Created,
            MaxRetryCount = 3
        };

        // Act
        var result = await _storage.StoreJobAsync(job);

        // Assert
        result.ShouldBe(job.Id);
        var storedJob = await _context.Jobs.FindAsync(job.Id);
        storedJob.ShouldNotBeNull();
        storedJob.Id.ShouldBe(job.Id);
        storedJob.TypeName.ShouldBe(job.TypeName);
        storedJob.Arguments.ShouldBe(job.Arguments);
        storedJob.State.ShouldBe(job.State);
        storedJob.MaxRetryCount.ShouldBe(job.MaxRetryCount);
    }

    [Fact]
    public async Task UpdateJobAsync_ShouldUpdateExistingJob()
    {
        // Arrange
        var job = new Job
        {
            Id = "test-job-2",
            TypeName = "TestJob",
            State = JobState.Created
        };

        await _storage.StoreJobAsync(job);

        // Modify job
        job.State = JobState.Succeeded;
        job.Result = "Job completed successfully";
        job.ExecutedAt = DateTimeOffset.UtcNow;

        // Act
        await _storage.UpdateJobAsync(job);

        // Assert
        var updatedJob = await _context.Jobs.FindAsync(job.Id);
        updatedJob.ShouldNotBeNull();
        updatedJob.State.ShouldBe(JobState.Succeeded);
        updatedJob.Result.ShouldBe("Job completed successfully");
        updatedJob.ExecutedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateJobAsync_WithNonExistentJob_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = new Job
        {
            Id = "non-existent-job",
            TypeName = "TestJob"
        };

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => _storage.UpdateJobAsync(job));
    }

    [Fact]
    public async Task GetJobAsync_ShouldReturnJob()
    {
        // Arrange
        var job = new Job
        {
            Id = "test-job-3",
            TypeName = "TestJob",
            Arguments = "{\"test\": \"value\"}",
            State = JobState.Scheduled,
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        await _storage.StoreJobAsync(job);

        // Act
        var result = await _storage.GetJobAsync(job.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(job.Id);
        result.TypeName.ShouldBe(job.TypeName);
        result.Arguments.ShouldBe(job.Arguments);
        result.State.ShouldBe(job.State);
        result.ScheduledAt.ShouldBe(job.ScheduledAt);
    }

    [Fact]
    public async Task GetJobAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = await _storage.GetJobAsync("non-existent-id");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetScheduledJobsAsync_ShouldReturnScheduledJobs()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var jobs = new[]
        {
            new Job { Id = "job1", TypeName = "TestJob", State = JobState.Scheduled, ScheduledAt = now.AddMinutes(-10) },
            new Job { Id = "job2", TypeName = "TestJob", State = JobState.Scheduled, ScheduledAt = now.AddMinutes(-5) },
            new Job { Id = "job3", TypeName = "TestJob", State = JobState.Scheduled, ScheduledAt = now.AddMinutes(10) }, // Future
            new Job { Id = "job4", TypeName = "TestJob", State = JobState.Created } // Not scheduled
        };

        foreach (var job in jobs)
        {
            await _storage.StoreJobAsync(job);
        }

        // Act
        var result = await _storage.GetScheduledJobsAsync(10);

        // Assert
        var scheduledJobs = result.ToList();
        scheduledJobs.Count.ShouldBe(2);
        scheduledJobs.ShouldContain(j => j.Id == "job1");
        scheduledJobs.ShouldContain(j => j.Id == "job2");
        scheduledJobs.ShouldNotContain(j => j.Id == "job3"); // Future job
        scheduledJobs.ShouldNotContain(j => j.Id == "job4"); // Not scheduled
    }

    [Fact]
    public async Task GetJobsByStateAsync_ShouldReturnJobsWithState()
    {
        // Arrange
        var jobs = new[]
        {
            new Job { Id = "job1", TypeName = "TestJob", State = JobState.Created },
            new Job { Id = "job2", TypeName = "TestJob", State = JobState.Created },
            new Job { Id = "job3", TypeName = "TestJob", State = JobState.Scheduled },
            new Job { Id = "job4", TypeName = "TestJob", State = JobState.Succeeded }
        };

        foreach (var job in jobs)
        {
            await _storage.StoreJobAsync(job);
        }

        // Act
        var result = await _storage.GetJobsByStateAsync(JobState.Created, 10);

        // Assert
        var createdJobs = result.ToList();
        createdJobs.Count.ShouldBe(2);
        createdJobs.ShouldAllBe(j => j.State == JobState.Created);
    }

    [Fact]
    public async Task DeleteJobAsync_ShouldRemoveJob()
    {
        // Arrange
        var job = new Job
        {
            Id = "job-to-delete",
            TypeName = "TestJob",
            State = JobState.Created
        };

        await _storage.StoreJobAsync(job);

        // Act
        await _storage.DeleteJobAsync(job.Id);

        // Assert
        var deletedJob = await _context.Jobs.FindAsync(job.Id);
        deletedJob.ShouldBeNull();
    }

    [Fact]
    public async Task GetJobCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var jobs = new[]
        {
            new Job { Id = "job1", TypeName = "TestJob", State = JobState.Created },
            new Job { Id = "job2", TypeName = "TestJob", State = JobState.Created },
            new Job { Id = "job3", TypeName = "TestJob", State = JobState.Scheduled },
        };

        foreach (var job in jobs)
        {
            await _storage.StoreJobAsync(job);
        }

        // Act
        var createdCount = await _storage.GetJobCountAsync(JobState.Created);
        var scheduledCount = await _storage.GetJobCountAsync(JobState.Scheduled);
        var succeededCount = await _storage.GetJobCountAsync(JobState.Succeeded);

        // Assert
        createdCount.ShouldBe(2);
        scheduledCount.ShouldBe(1);
        succeededCount.ShouldBe(0);
    }

    [Fact]
    public async Task StoreBatchAsync_ShouldStoreAllJobs()
    {
        // Arrange
        var batchId = "test-batch";
        var jobs = new[]
        {
            new Job { Id = "batch-job1", TypeName = "TestJob", State = JobState.Created, BatchId = batchId },
            new Job { Id = "batch-job2", TypeName = "TestJob", State = JobState.Created, BatchId = batchId }
        };

        // Act
        await _storage.StoreBatchAsync(batchId, jobs);

        // Assert
        var storedJobs = await _context.Jobs.Where(j => j.BatchId == batchId).ToListAsync();
        storedJobs.Count.ShouldBe(2);
        storedJobs.ShouldAllBe(j => j.BatchId == batchId);
    }

    [Fact]
    public async Task GetBatchJobsAsync_ShouldReturnBatchJobs()
    {
        // Arrange
        var batchId = "test-batch";
        var jobs = new[]
        {
            new Job { Id = "batch-job1", TypeName = "TestJob", State = JobState.Created, BatchId = batchId },
            new Job { Id = "batch-job2", TypeName = "TestJob", State = JobState.Created, BatchId = batchId },
            new Job { Id = "other-job", TypeName = "TestJob", State = JobState.Created, BatchId = "other-batch" }
        };

        foreach (var job in jobs)
        {
            await _storage.StoreJobAsync(job);
        }

        // Act
        var result = await _storage.GetBatchJobsAsync(batchId);

        // Assert
        var batchJobs = result.ToList();
        batchJobs.Count.ShouldBe(2);
        batchJobs.ShouldAllBe(j => ((Job)j).BatchId == batchId);
    }

    [Fact]
    public async Task StoreContinuationAsync_ShouldStoreContinuationJob()
    {
        // Arrange
        var parentJobId = "parent-job";
        var continuationJob = new Job
        {
            Id = "continuation-job",
            TypeName = "TestJob",
            State = JobState.AwaitingContinuation
        };

        // Act
        await _storage.StoreContinuationAsync(parentJobId, continuationJob);

        // Assert
        var storedJob = await _context.Jobs.FindAsync(continuationJob.Id);
        storedJob.ShouldNotBeNull();
        storedJob.ParentJobId.ShouldBe(parentJobId);
    }

    [Fact]
    public async Task GetContinuationsAsync_ShouldReturnContinuationJobs()
    {
        // Arrange
        var parentJobId = "parent-job";
        var continuationJob = new Job
        {
            Id = "continuation-job",
            TypeName = "TestJob",
            State = JobState.AwaitingContinuation,
            ParentJobId = parentJobId
        };

        await _storage.StoreJobAsync(continuationJob);

        // Act
        var result = await _storage.GetContinuationsAsync(parentJobId);

        // Assert
        var continuations = result.ToList();
        continuations.Count.ShouldBe(1);
        continuations[0].Id.ShouldBe(continuationJob.Id);
    }

    [Fact]
    public async Task StoreRecurringJobAsync_ShouldStoreRecurringJob()
    {
        // Arrange
        var recurringJobId = "recurring-job";
        var cronExpression = "0 */5 * * * *";
        var jobTemplate = new Job
        {
            Id = "template",
            TypeName = "TestJob",
            MaxRetryCount = 3
        };

        // Act
        await _storage.StoreRecurringJobAsync(recurringJobId, cronExpression, jobTemplate);

        // Assert
        var storedJob = await _context.RecurringJobs.FindAsync(recurringJobId);
        storedJob.ShouldNotBeNull();
        storedJob.CronExpression.ShouldBe(cronExpression);
        storedJob.JobTypeName.ShouldBe(jobTemplate.TypeName);
        storedJob.MaxRetryCount.ShouldBe(jobTemplate.MaxRetryCount);
        storedJob.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetRecurringJobsAsync_ShouldReturnEnabledRecurringJobs()
    {
        // Arrange
        var recurringJobId = "recurring-job";
        var cronExpression = "0 */5 * * * *";
        var jobTemplate = new Job
        {
            Id = "template",
            TypeName = "TestJob",
            MaxRetryCount = 3
        };

        await _storage.StoreRecurringJobAsync(recurringJobId, cronExpression, jobTemplate);

        // Act
        var result = await _storage.GetRecurringJobsAsync();

        // Assert
        var recurringJobs = result.ToList();
        recurringJobs.Count.ShouldBe(1);
        var recurringJob = recurringJobs[0];
        recurringJob.Id.ShouldBe(recurringJobId);
        recurringJob.CronExpression.ShouldBe(cronExpression);
        recurringJob.JobTemplate.TypeName.ShouldBe(jobTemplate.TypeName);
        recurringJob.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveRecurringJobAsync_ShouldRemoveRecurringJob()
    {
        // Arrange
        var recurringJobId = "recurring-job";
        var cronExpression = "0 */5 * * * *";
        var jobTemplate = new Job
        {
            Id = "template",
            TypeName = "TestJob"
        };

        await _storage.StoreRecurringJobAsync(recurringJobId, cronExpression, jobTemplate);

        // Act
        await _storage.RemoveRecurringJobAsync(recurringJobId);

        // Assert
        var removedJob = await _context.RecurringJobs.FindAsync(recurringJobId);
        removedJob.ShouldBeNull();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}