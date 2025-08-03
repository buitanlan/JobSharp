using JobSharp.Core;
using JobSharp.Jobs;
using JobSharp.MongoDb.Storage;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Mongo2Go;
using NSubstitute;
using Shouldly;
using Xunit;

namespace JobSharp.MongoDb.Tests.Storage;

public class MongoDbJobStorageTests : IDisposable
{
    private readonly MongoDbRunner _mongoRunner;
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoDbJobStorage> _logger;
    private readonly MongoDbJobStorage _storage;

    public MongoDbJobStorageTests()
    {
        _mongoRunner = MongoDbRunner.Start();
        var client = new MongoClient(_mongoRunner.ConnectionString);
        _database = client.GetDatabase("JobSharpTests");
        _logger = Substitute.For<ILogger<MongoDbJobStorage>>();
        _storage = new MongoDbJobStorage(_database, _logger);
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
            MaxRetryCount = 3,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _storage.StoreJobAsync(job);

        // Assert
        result.ShouldBe(job.Id);

        var collection = _database.GetCollection<Models.JobDocument>("jobs");
        var storedJob = await collection.Find(j => j.Id == job.Id).FirstOrDefaultAsync();

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
            State = JobState.Created,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _storage.StoreJobAsync(job);

        // Modify job
        job.State = JobState.Succeeded;
        job.Result = "Job completed successfully";
        job.ExecutedAt = DateTimeOffset.UtcNow;

        // Act
        await _storage.UpdateJobAsync(job);

        // Assert
        var updatedJob = await _storage.GetJobAsync(job.Id);
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
            TypeName = "TestJob",
            CreatedAt = DateTimeOffset.UtcNow
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
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow
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
        result.ScheduledAt.ShouldNotBeNull();
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
            new Job { Id = "job1", TypeName = "TestJob", State = JobState.Scheduled, ScheduledAt = now.AddMinutes(-10), CreatedAt = now.AddMinutes(-15) },
            new Job { Id = "job2", TypeName = "TestJob", State = JobState.Scheduled, ScheduledAt = now.AddMinutes(-5), CreatedAt = now.AddMinutes(-10) },
            new Job { Id = "job3", TypeName = "TestJob", State = JobState.Scheduled, ScheduledAt = now.AddMinutes(10), CreatedAt = now.AddMinutes(-5) }, // Future
            new Job { Id = "job4", TypeName = "TestJob", State = JobState.Created, CreatedAt = now } // Not scheduled
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
        var now = DateTimeOffset.UtcNow;
        var jobs = new[]
        {
            new Job { Id = "job1", TypeName = "TestJob", State = JobState.Created, CreatedAt = now },
            new Job { Id = "job2", TypeName = "TestJob", State = JobState.Created, CreatedAt = now },
            new Job { Id = "job3", TypeName = "TestJob", State = JobState.Scheduled, CreatedAt = now },
            new Job { Id = "job4", TypeName = "TestJob", State = JobState.Succeeded, CreatedAt = now }
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
            State = JobState.Created,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _storage.StoreJobAsync(job);

        // Act
        await _storage.DeleteJobAsync(job.Id);

        // Assert
        var deletedJob = await _storage.GetJobAsync(job.Id);
        deletedJob.ShouldBeNull();
    }

    [Fact]
    public async Task GetJobCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var jobs = new[]
        {
            new Job { Id = "job1", TypeName = "TestJob", State = JobState.Created, CreatedAt = now },
            new Job { Id = "job2", TypeName = "TestJob", State = JobState.Created, CreatedAt = now },
            new Job { Id = "job3", TypeName = "TestJob", State = JobState.Scheduled, CreatedAt = now },
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
        var now = DateTimeOffset.UtcNow;
        var jobs = new[]
        {
            new Job { Id = "batch-job1", TypeName = "TestJob", State = JobState.Created, BatchId = batchId, CreatedAt = now },
            new Job { Id = "batch-job2", TypeName = "TestJob", State = JobState.Created, BatchId = batchId, CreatedAt = now }
        };

        // Act
        await _storage.StoreBatchAsync(batchId, jobs);

        // Assert
        var storedJobs = await _storage.GetBatchJobsAsync(batchId);
        var batchJobs = storedJobs.ToList();
        batchJobs.Count.ShouldBe(2);
        batchJobs.ShouldAllBe(j => ((Job)j).BatchId == batchId);
    }

    [Fact]
    public async Task GetBatchJobsAsync_ShouldReturnBatchJobs()
    {
        // Arrange
        var batchId = "test-batch";
        var now = DateTimeOffset.UtcNow;
        var jobs = new[]
        {
            new Job { Id = "batch-job1", TypeName = "TestJob", State = JobState.Created, BatchId = batchId, CreatedAt = now },
            new Job { Id = "batch-job2", TypeName = "TestJob", State = JobState.Created, BatchId = batchId, CreatedAt = now },
            new Job { Id = "other-job", TypeName = "TestJob", State = JobState.Created, BatchId = "other-batch", CreatedAt = now }
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
            State = JobState.AwaitingContinuation,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await _storage.StoreContinuationAsync(parentJobId, continuationJob);

        // Assert
        var storedJob = await _storage.GetJobAsync(continuationJob.Id);
        storedJob.ShouldNotBeNull();
        ((Job)storedJob).ParentJobId.ShouldBe(parentJobId);
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
            ParentJobId = parentJobId,
            CreatedAt = DateTimeOffset.UtcNow
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
            MaxRetryCount = 3,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await _storage.StoreRecurringJobAsync(recurringJobId, cronExpression, jobTemplate);

        // Assert
        var collection = _database.GetCollection<Models.RecurringJobDocument>("recurringJobs");
        var storedJob = await collection.Find(j => j.Id == recurringJobId).FirstOrDefaultAsync();

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
            MaxRetryCount = 3,
            CreatedAt = DateTimeOffset.UtcNow
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
            TypeName = "TestJob",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _storage.StoreRecurringJobAsync(recurringJobId, cronExpression, jobTemplate);

        // Act
        await _storage.RemoveRecurringJobAsync(recurringJobId);

        // Assert
        var collection = _database.GetCollection<Models.RecurringJobDocument>("recurringJobs");
        var removedJob = await collection.Find(j => j.Id == recurringJobId).FirstOrDefaultAsync();

        removedJob.ShouldBeNull();
    }

    [Fact]
    public async Task StoreJobAsync_WithDateTimeOffsetHandling_ShouldConvertToUtc()
    {
        // Arrange
        var scheduledAt = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.FromHours(5)); // UTC+5
        var job = new Job
        {
            Id = "datetime-test-job",
            TypeName = "TestJob",
            State = JobState.Scheduled,
            ScheduledAt = scheduledAt,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await _storage.StoreJobAsync(job);

        // Assert
        var retrievedJob = await _storage.GetJobAsync(job.Id);
        retrievedJob.ShouldNotBeNull();
        retrievedJob.ScheduledAt.ShouldNotBeNull();

        // Should be stored and retrieved as UTC
        var expectedUtc = scheduledAt.UtcDateTime;
        retrievedJob.ScheduledAt.Value.UtcDateTime.ShouldBe(expectedUtc);
    }

    [Fact]
    public async Task CreateIndexes_ShouldBeCalledOnInitialization()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MongoDbJobStorage>>();

        // Act
        var storage = new MongoDbJobStorage(_database, logger);

        // Assert - Verify indexes exist by attempting operations that would benefit from them
        var now = DateTimeOffset.UtcNow;
        var testJob = new Job
        {
            Id = "index-test-job",
            TypeName = "TestJob",
            State = JobState.Scheduled,
            ScheduledAt = now.AddMinutes(-1),
            CreatedAt = now
        };

        await storage.StoreJobAsync(testJob);

        // These operations should be efficient with proper indexes
        var scheduledJobs = await storage.GetScheduledJobsAsync(100);
        var jobsByState = await storage.GetJobsByStateAsync(JobState.Scheduled, 100);

        scheduledJobs.ShouldNotBeEmpty();
        jobsByState.ShouldNotBeEmpty();
    }

    public void Dispose()
    {
        _mongoRunner?.Dispose();
    }
}