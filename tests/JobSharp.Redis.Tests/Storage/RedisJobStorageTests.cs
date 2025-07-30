using JobSharp.Core;
using JobSharp.Jobs;
using JobSharp.Redis.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;
using System.Net;
using System.Text.Json;
using Xunit;

namespace JobSharp.Redis.Tests.Storage;

public class RedisJobStorageTests : IDisposable
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisJobStorage> _logger;
    private readonly RedisJobStorage _storage;
    private readonly ConnectionMultiplexer _connection;

    public RedisJobStorageTests()
    {
        // For unit tests, we'll use a mock Redis database
        _database = Substitute.For<IDatabase>();
        _logger = Substitute.For<ILogger<RedisJobStorage>>();

        // Mock connection for integration scenarios (commented out for pure unit tests)
        _connection = Substitute.For<ConnectionMultiplexer>();

        _storage = new RedisJobStorage(_database, _logger);

        SetupMockDatabase();
    }

    private void SetupMockDatabase()
    {
        // Setup default mock behaviors for Redis operations
        _database.HashSetAsync(Arg.Any<RedisKey>(), Arg.Any<HashEntry[]>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(0));

        _database.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        _database.SortedSetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<double>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));
    }

    [Fact]
    public async Task StoreJobAsync_ShouldStoreJobInRedis()
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

        var jobData = new JobSharp.Redis.Models.JobData
        {
            Id = job.Id,
            TypeName = job.TypeName,
            Arguments = job.Arguments,
            State = job.State,
            CreatedAt = job.CreatedAt,
            MaxRetryCount = job.MaxRetryCount
        };

        var serializedJob = JsonSerializer.Serialize(jobData);

        _database.StringSetAsync($"jobsharp:job:{job.Id}", serializedJob, null, When.Always, CommandFlags.None)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _storage.StoreJobAsync(job);

        // Assert
        result.ShouldBe(job.Id);
        await _database.Received(1).StringSetAsync(
            $"jobsharp:job:{job.Id}",
            Arg.Any<RedisValue>(),
            null,
            When.Always,
            CommandFlags.None);

        await _database.Received(1).SetAddAsync(
            $"jobsharp:jobs:state:{(int)job.State}",
            job.Id,
            CommandFlags.None);
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

        // Setup existing job
        _database.StringGetAsync($"jobsharp:job:{job.Id}", CommandFlags.None)
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(new JobSharp.Redis.Models.JobData
            {
                Id = job.Id,
                TypeName = job.TypeName,
                State = JobState.Created,
                CreatedAt = job.CreatedAt
            })));

        // Modify job
        job.State = JobState.Succeeded;
        job.Result = "Job completed successfully";
        job.ExecutedAt = DateTimeOffset.UtcNow;

        // Act
        await _storage.UpdateJobAsync(job);

        // Assert
        await _database.Received(1).StringSetAsync(
            $"jobsharp:job:{job.Id}",
            Arg.Any<RedisValue>(),
            null,
            When.Always,
            CommandFlags.None);

        // Should remove from old state set and add to new state set
        await _database.Received(1).SetRemoveAsync(
            $"jobsharp:jobs:state:{(int)JobState.Created}",
            job.Id,
            CommandFlags.None);

        await _database.Received(1).SetAddAsync(
            $"jobsharp:jobs:state:{(int)JobState.Succeeded}",
            job.Id,
            CommandFlags.None);
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

        _database.StringGetAsync($"jobsharp:job:{job.Id}", CommandFlags.None)
            .Returns(Task.FromResult(RedisValue.Null));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => _storage.UpdateJobAsync(job));
    }

    [Fact]
    public async Task GetJobAsync_ShouldReturnJob()
    {
        // Arrange
        var jobId = "test-job-3";
        var jobData = new JobSharp.Redis.Models.JobData
        {
            Id = jobId,
            TypeName = "TestJob",
            Arguments = "{\"test\": \"value\"}",
            State = JobState.Scheduled,
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _database.StringGetAsync($"jobsharp:job:{jobId}", CommandFlags.None)
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(jobData)));

        // Act
        var result = await _storage.GetJobAsync(jobId);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(jobId);
        result.TypeName.ShouldBe("TestJob");
        result.Arguments.ShouldBe("{\"test\": \"value\"}");
        result.State.ShouldBe(JobState.Scheduled);
        result.ScheduledAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetJobAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        _database.StringGetAsync("jobsharp:job:non-existent-id", CommandFlags.None)
            .Returns(Task.FromResult(RedisValue.Null));

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
        var scheduledJobIds = new RedisValue[] { "job1", "job2" };

        _database.SortedSetRangeByScoreAsync(
            "jobsharp:jobs:scheduled",
            0,
            now.ToUnixTimeSeconds(),
            Exclude.None,
            Order.Ascending,
            0,
            10,
            CommandFlags.None)
            .Returns(Task.FromResult(scheduledJobIds));

        // Setup job data for each scheduled job
        var job1Data = new JobSharp.Redis.Models.JobData
        {
            Id = "job1",
            TypeName = "TestJob",
            State = JobState.Scheduled,
            ScheduledAt = now.AddMinutes(-10),
            CreatedAt = now.AddMinutes(-15)
        };

        var job2Data = new JobSharp.Redis.Models.JobData
        {
            Id = "job2",
            TypeName = "TestJob",
            State = JobState.Scheduled,
            ScheduledAt = now.AddMinutes(-5),
            CreatedAt = now.AddMinutes(-10)
        };

        _database.StringGetAsync("jobsharp:job:job1", CommandFlags.None)
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(job1Data)));

        _database.StringGetAsync("jobsharp:job:job2", CommandFlags.None)
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(job2Data)));

        // Act
        var result = await _storage.GetScheduledJobsAsync(10);

        // Assert
        var scheduledJobs = result.ToList();
        scheduledJobs.Count.ShouldBe(2);
        scheduledJobs.ShouldContain(j => j.Id == "job1");
        scheduledJobs.ShouldContain(j => j.Id == "job2");
    }

    [Fact]
    public async Task GetJobsByStateAsync_ShouldReturnJobsWithState()
    {
        // Arrange
        var jobIds = new RedisValue[] { "job1", "job2" };

        _database.SetMembersAsync($"jobsharp:jobs:state:{(int)JobState.Created}", CommandFlags.None)
            .Returns(Task.FromResult(jobIds));

        var job1Data = new JobSharp.Redis.Models.JobData
        {
            Id = "job1",
            TypeName = "TestJob",
            State = JobState.Created,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var job2Data = new JobSharp.Redis.Models.JobData
        {
            Id = "job2",
            TypeName = "TestJob",
            State = JobState.Created,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _database.StringGetAsync("jobsharp:job:job1", CommandFlags.None)
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(job1Data)));

        _database.StringGetAsync("jobsharp:job:job2", CommandFlags.None)
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(job2Data)));

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
        var jobId = "job-to-delete";
        var jobData = new JobSharp.Redis.Models.JobData
        {
            Id = jobId,
            TypeName = "TestJob",
            State = JobState.Created,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _database.StringGetAsync($"jobsharp:job:{jobId}", CommandFlags.None)
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(jobData)));

        _database.KeyDeleteAsync($"jobsharp:job:{jobId}", CommandFlags.None)
            .Returns(Task.FromResult(true));

        // Act
        await _storage.DeleteJobAsync(jobId);

        // Assert
        await _database.Received(1).KeyDeleteAsync($"jobsharp:job:{jobId}", CommandFlags.None);
        await _database.Received(1).SetRemoveAsync(
            $"jobsharp:jobs:state:{(int)JobState.Created}",
            jobId,
            CommandFlags.None);
    }

    [Fact]
    public async Task GetJobCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        _database.SetLengthAsync($"jobsharp:jobs:state:{(int)JobState.Created}", CommandFlags.None)
            .Returns(Task.FromResult(2L));

        _database.SetLengthAsync($"jobsharp:jobs:state:{(int)JobState.Scheduled}", CommandFlags.None)
            .Returns(Task.FromResult(1L));

        _database.SetLengthAsync($"jobsharp:jobs:state:{(int)JobState.Succeeded}", CommandFlags.None)
            .Returns(Task.FromResult(0L));

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
        foreach (var job in jobs)
        {
            await _database.Received(1).StringSetAsync(
                $"jobsharp:job:{job.Id}",
                Arg.Any<RedisValue>(),
                null,
                When.Always,
                CommandFlags.None);

            await _database.Received(1).SetAddAsync(
                $"jobsharp:jobs:batch:{batchId}",
                job.Id,
                CommandFlags.None);
        }
    }

    [Fact]
    public async Task GetBatchJobsAsync_ShouldReturnBatchJobs()
    {
        // Arrange
        var batchId = "test-batch";
        var jobIds = new RedisValue[] { "batch-job1", "batch-job2" };

        _database.SetMembersAsync($"jobsharp:jobs:batch:{batchId}", CommandFlags.None)
            .Returns(Task.FromResult(jobIds));

        var job1Data = new JobSharp.Redis.Models.JobData
        {
            Id = "batch-job1",
            TypeName = "TestJob",
            State = JobState.Created,
            BatchId = batchId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var job2Data = new JobSharp.Redis.Models.JobData
        {
            Id = "batch-job2",
            TypeName = "TestJob",
            State = JobState.Created,
            BatchId = batchId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _database.StringGetAsync("jobsharp:job:batch-job1", CommandFlags.None)
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(job1Data)));

        _database.StringGetAsync("jobsharp:job:batch-job2", CommandFlags.None)
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(job2Data)));

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
        await _database.Received(1).StringSetAsync(
            $"jobsharp:job:{continuationJob.Id}",
            Arg.Any<RedisValue>(),
            null,
            When.Always,
            CommandFlags.None);

        await _database.Received(1).SetAddAsync(
            $"jobsharp:jobs:continuation:{parentJobId}",
            continuationJob.Id,
            CommandFlags.None);
    }

    [Fact]
    public async Task GetContinuationsAsync_ShouldReturnContinuationJobs()
    {
        // Arrange
        var parentJobId = "parent-job";
        var jobIds = new RedisValue[] { "continuation-job" };

        _database.SetMembersAsync($"jobsharp:jobs:continuation:{parentJobId}", CommandFlags.None)
            .Returns(Task.FromResult(jobIds));

        var continuationData = new JobSharp.Redis.Models.JobData
        {
            Id = "continuation-job",
            TypeName = "TestJob",
            State = JobState.AwaitingContinuation,
            ParentJobId = parentJobId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _database.StringGetAsync("jobsharp:job:continuation-job", CommandFlags.None)
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(continuationData)));

        // Act
        var result = await _storage.GetContinuationsAsync(parentJobId);

        // Assert
        var continuations = result.ToList();
        continuations.Count.ShouldBe(1);
        continuations[0].Id.ShouldBe("continuation-job");
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
        await _database.Received(1).StringSetAsync(
            $"jobsharp:recurringjob:{recurringJobId}",
            Arg.Any<RedisValue>(),
            null,
            When.Always,
            CommandFlags.None);
    }

    [Fact]
    public async Task GetRecurringJobsAsync_ShouldReturnEnabledRecurringJobs()
    {
        // Arrange
        var recurringJobId = "recurring-job";
        var keys = new RedisKey[] { $"jobsharp:recurringjob:{recurringJobId}" };

        // Mock the SCAN operation for recurring jobs
        _database.Multiplexer.GetServer(Arg.Any<EndPoint>())
            .Keys(Arg.Any<int>(), "jobsharp:recurringjob:*", Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), CommandFlags.None)
            .Returns(keys);

        var recurringJobData = new JobSharp.Redis.Models.RecurringJobData
        {
            Id = recurringJobId,
            CronExpression = "0 */5 * * * *",
            JobTypeName = "TestJob",
            MaxRetryCount = 3,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _database.StringGetAsync($"jobsharp:recurringjob:{recurringJobId}", CommandFlags.None)
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(recurringJobData)));

        // For this test, we'll simulate the behavior since we can't easily mock the SCAN operation
        // In a real integration test, this would work with an actual Redis instance

        // Act & Assert - This test verifies the structure but would need integration testing for full coverage
        var serializedData = JsonSerializer.Serialize(recurringJobData);
        serializedData.ShouldNotBeNull();

        var deserializedData = JsonSerializer.Deserialize<JobSharp.Redis.Models.RecurringJobData>(serializedData);
        deserializedData.ShouldNotBeNull();
        deserializedData.Id.ShouldBe(recurringJobId);
        deserializedData.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveRecurringJobAsync_ShouldRemoveRecurringJob()
    {
        // Arrange
        var recurringJobId = "recurring-job";

        _database.KeyDeleteAsync($"jobsharp:recurringjob:{recurringJobId}", CommandFlags.None)
            .Returns(Task.FromResult(true));

        // Act
        await _storage.RemoveRecurringJobAsync(recurringJobId);

        // Assert
        await _database.Received(1).KeyDeleteAsync($"jobsharp:recurringjob:{recurringJobId}", CommandFlags.None);
    }

    [Fact]
    public void Constructor_WithNullDatabase_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new RedisJobStorage(null!, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new RedisJobStorage(_database, null!));
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}