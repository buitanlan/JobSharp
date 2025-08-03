using JobSharp.Core;
using JobSharp.Jobs;
using JobSharp.Redis.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;
using System.Text.Json;
using Xunit;

namespace JobSharp.Redis.Tests.Storage;

public class RedisJobStorageTests : IDisposable
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisJobStorage> _logger;
    private readonly RedisJobStorage _storage;
    private readonly ConnectionMultiplexer? _connection;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisJobStorageTests()
    {
        _database = Substitute.For<IDatabase>();
        _logger = Substitute.For<ILogger<RedisJobStorage>>();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        _connection = null;
        _storage = new RedisJobStorage(_database, _logger);
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

        // Act
        var result = await _storage.StoreJobAsync(job);

        // Assert
        result.ShouldBe(job.Id);
        await _database.Received(1).StringSetAsync(
            Arg.Is<RedisKey>(k => k == $"jobsharp:job:{job.Id}"),
            Arg.Any<RedisValue>());

        await _database.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k == $"jobsharp:job:state:{(int)job.State}"),
            Arg.Is<RedisValue>(v => v == job.Id));
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

        var jobData = new Models.JobData
        {
            Id = job.Id,
            TypeName = job.TypeName,
            State = JobState.Created,
            CreatedAt = job.CreatedAt
        };

        _database.StringGetAsync($"jobsharp:job:{job.Id}")
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(jobData, _jsonOptions)));

        job.State = JobState.Succeeded;
        job.Result = "Job completed successfully";
        job.ExecutedAt = DateTimeOffset.UtcNow;

        // Act
        await _storage.UpdateJobAsync(job);

        // Assert
        await _database.Received(1).StringSetAsync(
            Arg.Is<RedisKey>(k => k == $"jobsharp:job:{job.Id}"),
            Arg.Any<RedisValue>());

        await _database.Received(1).SetRemoveAsync(
            Arg.Is<RedisKey>(k => k == $"jobsharp:job:state:{(int)JobState.Created}"),
            Arg.Is<RedisValue>(v => v == job.Id));

        await _database.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k == $"jobsharp:job:state:{(int)JobState.Succeeded}"),
            Arg.Is<RedisValue>(v => v == job.Id));
    }

    [Fact]
    public async Task UpdateJobAsync_WithNonExistentJob_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = new Job { Id = "non-existent-job", TypeName = "TestJob" };

        _database.StringGetAsync(Arg.Any<RedisKey>())
            .Returns(Task.FromResult(RedisValue.Null));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => _storage.UpdateJobAsync(job));
    }

    [Fact]
    public async Task GetJobAsync_ShouldReturnJob()
    {
        // Arrange
        var jobId = "test-job-3";
        var jobData = new Models.JobData { Id = jobId, TypeName = "TestJob" };

        _database.StringGetAsync($"jobsharp:job:{jobId}")
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(jobData, _jsonOptions)));

        // Act
        var result = await _storage.GetJobAsync(jobId);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(jobId);
    }

    [Fact]
    public async Task GetJobAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        _database.StringGetAsync(Arg.Any<RedisKey>())
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
        var scheduledJobIds = new RedisValue[] { "job1", "job2" };

        _database.SortedSetRangeByScoreAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<Exclude>())
            .Returns(Task.FromResult(scheduledJobIds));

        _database.StringGetAsync("jobsharp:job:job1")
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(new Models.JobData { Id = "job1", TypeName = "TestJob", State = JobState.Scheduled }, _jsonOptions)));

        _database.StringGetAsync("jobsharp:job:job2")
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(new Models.JobData { Id = "job2", TypeName = "TestJob", State = JobState.Scheduled }, _jsonOptions)));

        // Act
        _logger.LogInformation("Calling GetScheduledJobsAsync with batch size {BatchSize}", 100);
        var result = await _storage.GetScheduledJobsAsync(100);
        _logger.LogInformation("GetScheduledJobsAsync returned {JobCount} jobs", result.Count());

        // Assert
        result.Count().ShouldBe(2);
    }

    [Fact]
    public async Task GetJobsByStateAsync_ShouldReturnJobsWithState()
    {
        // Arrange
        var jobIds = new RedisValue[] { "job1", "job2" };

        _database.SetRandomMembersAsync($"jobsharp:job:state:{(int)JobState.Created}", 100)
            .Returns(Task.FromResult(jobIds));

        _database.StringGetAsync("jobsharp:job:job1")
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(new Models.JobData { Id = "job1", TypeName = "TestJob", State = JobState.Created }, _jsonOptions)));

        _database.StringGetAsync("jobsharp:job:job2")
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(new Models.JobData { Id = "job2", TypeName = "TestJob", State = JobState.Created }, _jsonOptions)));

        // Act
        var result = await _storage.GetJobsByStateAsync(JobState.Created, 100);

        // Assert
        result.Count().ShouldBe(2);
    }

    [Fact]
    public async Task DeleteJobAsync_ShouldRemoveJob()
    {
        // Arrange
        var jobId = "job-to-delete";
        var jobData = new Models.JobData { Id = jobId, TypeName = "TestJob", State = JobState.Created };

        _database.StringGetAsync($"jobsharp:job:{jobId}")
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(jobData, _jsonOptions)));

        // Act
        await _storage.DeleteJobAsync(jobId);

        // Assert
        await _database.Received(1).KeyDeleteAsync($"jobsharp:job:{jobId}");
        await _database.Received(1).SetRemoveAsync($"jobsharp:job:state:{(int)JobState.Created}", jobId);
    }

    [Fact]
    public async Task GetJobCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        _database.SetLengthAsync($"jobsharp:job:state:{(int)JobState.Created}")
            .Returns(Task.FromResult(2L));

        // Act
        var result = await _storage.GetJobCountAsync(JobState.Created);

        // Assert
        result.ShouldBe(2);
    }

    [Fact]
    public async Task StoreBatchAsync_ShouldStoreAllJobs()
    {
        // Arrange
        var batchId = "test-batch";
        var jobs = new[]
        {
            new Job { Id = "batch-job1", TypeName = "TestJob", BatchId = batchId },
            new Job { Id = "batch-job2", TypeName = "TestJob", BatchId = batchId }
        };

        // Act
        await _storage.StoreBatchAsync(batchId, jobs);

        // Assert
        await _database.Received(2).StringSetAsync(Arg.Is<RedisKey>(k => k.ToString().StartsWith("jobsharp:job:")), Arg.Any<RedisValue>());
        await _database.Received(2).SetAddAsync(Arg.Is<RedisKey>(k => k.ToString().StartsWith("jobsharp:job:state:")), Arg.Any<RedisValue>());
        await _database.Received(2).SetAddAsync(Arg.Is<RedisKey>(k => k == $"jobsharp:jobs:batch:{batchId}"), Arg.Any<RedisValue>());
    }

    [Fact]
    public async Task GetBatchJobsAsync_ShouldReturnBatchJobs()
    {
        // Arrange
        var batchId = "test-batch";
        var jobIds = new RedisValue[] { "batch-job1", "batch-job2" };

        _database.SetMembersAsync($"jobsharp:jobs:batch:{batchId}")
            .Returns(Task.FromResult(jobIds));

        _database.StringGetAsync("jobsharp:job:batch-job1")
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(new Models.JobData { Id = "batch-job1", TypeName = "TestJob", BatchId = batchId }, _jsonOptions)));

        _database.StringGetAsync("jobsharp:job:batch-job2")
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(new Models.JobData { Id = "batch-job2", TypeName = "TestJob", BatchId = batchId }, _jsonOptions)));

        // Act
        var result = await _storage.GetBatchJobsAsync(batchId);

        // Assert
        result.Count().ShouldBe(2);
    }

    [Fact]
    public async Task StoreContinuationAsync_ShouldStoreContinuationJob()
    {
        // Arrange
        var parentJobId = "parent-job";
        var continuationJob = new Job { Id = "continuation-job", TypeName = "TestJob" };

        // Act
        await _storage.StoreContinuationAsync(parentJobId, continuationJob);

        // Assert
        await _database.Received(1).StringSetAsync(
            Arg.Is<RedisKey>(k => k == $"jobsharp:job:{continuationJob.Id}"),
            Arg.Any<RedisValue>());

        await _database.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k == $"jobsharp:jobs:continuation:{parentJobId}"),
            Arg.Is<RedisValue>(v => v == continuationJob.Id));
    }

    [Fact]
    public async Task GetContinuationsAsync_ShouldReturnContinuationJobs()
    {
        // Arrange
        var parentJobId = "parent-job";
        var jobIds = new RedisValue[] { "continuation-job" };

        _database.SetMembersAsync($"jobsharp:jobs:continuation:{parentJobId}")
            .Returns(Task.FromResult(jobIds));

        _database.StringGetAsync("jobsharp:job:continuation-job")
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(new Models.JobData { Id = "continuation-job", TypeName = "TestJob", State = JobState.AwaitingContinuation }, _jsonOptions)));

        // Act
        var result = await _storage.GetContinuationsAsync(parentJobId);

        // Assert
        result.Count().ShouldBe(1);
    }

    [Fact]
    public async Task StoreRecurringJobAsync_ShouldStoreRecurringJob()
    {
        // Arrange
        var recurringJobId = "recurring-job";
        var cronExpression = "0 */5 * * * *";
        var jobTemplate = new Job { Id = "template", TypeName = "TestJob" };

        // Act
        await _storage.StoreRecurringJobAsync(recurringJobId, cronExpression, jobTemplate);

        // Assert
        await _database.Received(1).StringSetAsync(
            Arg.Is<RedisKey>(k => k == $"jobsharp:recurringjob:{recurringJobId}"),
            Arg.Any<RedisValue>());

        await _database.Received(1).SetAddAsync(
            "jobsharp:recurring:all",
            recurringJobId);
    }

    [Fact]
    public async Task GetRecurringJobsAsync_ShouldReturnEnabledRecurringJobs()
    {
        // Arrange
        var recurringJobIds = new RedisValue[] { "recurring-job" };
        _database.SetMembersAsync("jobsharp:recurring:all")
            .Returns(Task.FromResult(recurringJobIds));

        var recurringJobData = new Models.RecurringJobData { Id = "recurring-job", CronExpression = "0 */5 * * * *", JobTypeName = "TestJob", IsEnabled = true };

        _database.StringGetAsync($"jobsharp:recurringjob:recurring-job")
            .Returns(Task.FromResult((RedisValue)JsonSerializer.Serialize(recurringJobData, _jsonOptions)));

        // Act
        var result = await _storage.GetRecurringJobsAsync();

        // Assert
        result.Count().ShouldBe(1);
    }

    [Fact]
    public async Task RemoveRecurringJobAsync_ShouldRemoveRecurringJob()
    {
        // Arrange
        var recurringJobId = "recurring-job";

        // Act
        await _storage.RemoveRecurringJobAsync(recurringJobId);

        // Assert
        await _database.Received(1).KeyDeleteAsync($"jobsharp:recurringjob:{recurringJobId}");
        await _database.Received(1).SetRemoveAsync("jobsharp:recurring:all", recurringJobId);
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
        GC.SuppressFinalize(this);
    }
}
