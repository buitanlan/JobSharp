using System.Data;
using JobSharp.Core;
using JobSharp.Dapper.Storage;
using JobSharp.Jobs;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using Dapper;

namespace JobSharp.Dapper.Tests.Storage;

public class DapperJobStorageTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly ILogger<DapperJobStorage> _logger;
    private readonly DapperJobStorage _storage;

    public DapperJobStorageTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _logger = Substitute.For<ILogger<DapperJobStorage>>();
        _storage = new DapperJobStorage(_connection, _logger);

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        // Create tables using SQLite schema
        var createTablesScript = @"
            CREATE TABLE Jobs (
                Id TEXT NOT NULL PRIMARY KEY,
                TypeName TEXT NOT NULL,
                Arguments TEXT NULL,
                State INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                ScheduledAt TEXT NULL,
                ExecutedAt TEXT NULL,
                RetryCount INTEGER NOT NULL DEFAULT 0,
                MaxRetryCount INTEGER NOT NULL DEFAULT 0,
                ErrorMessage TEXT NULL,
                Result TEXT NULL,
                BatchId TEXT NULL,
                ParentJobId TEXT NULL
            );

            CREATE TABLE RecurringJobs (
                Id TEXT NOT NULL PRIMARY KEY,
                CronExpression TEXT NOT NULL,
                JobTypeName TEXT NOT NULL,
                JobArguments TEXT NULL,
                MaxRetryCount INTEGER NOT NULL DEFAULT 0,
                NextExecution TEXT NULL,
                LastExecution TEXT NULL,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL
            );

            CREATE INDEX IX_Jobs_State ON Jobs (State);
            CREATE INDEX IX_Jobs_ScheduledAt ON Jobs (ScheduledAt);
            CREATE INDEX IX_Jobs_BatchId ON Jobs (BatchId);
            CREATE INDEX IX_Jobs_ParentJobId ON Jobs (ParentJobId);";

        _connection.Execute(createTablesScript);
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

        var storedJob = await _connection.QuerySingleOrDefaultAsync(
            "SELECT * FROM Jobs WHERE Id = @Id", new { Id = job.Id });

        storedJob.ShouldNotBeNull();
        ((string)storedJob.Id).ShouldBe(job.Id);
        ((string)storedJob.TypeName).ShouldBe(job.TypeName);
        ((string)storedJob.Arguments).ShouldBe(job.Arguments);
        ((int)storedJob.State).ShouldBe((int)job.State);
        ((int)storedJob.MaxRetryCount).ShouldBe(job.MaxRetryCount);
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
        var deletedJob = await _storage.GetJobAsync(job.Id);
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
        var storedJob = await _connection.QuerySingleOrDefaultAsync(
            "SELECT * FROM RecurringJobs WHERE Id = @Id",
            new { Id = recurringJobId });

        storedJob.ShouldNotBeNull();
        ((string)storedJob.CronExpression).ShouldBe(cronExpression);
        ((string)storedJob.JobTypeName).ShouldBe(jobTemplate.TypeName);
        ((int)storedJob.MaxRetryCount).ShouldBe(jobTemplate.MaxRetryCount);
        ((int)storedJob.IsEnabled).ShouldBe(1);
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
        var removedJob = await _connection.QuerySingleOrDefaultAsync(
            "SELECT * FROM RecurringJobs WHERE Id = @Id",
            new { Id = recurringJobId });

        removedJob.ShouldBeNull();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}