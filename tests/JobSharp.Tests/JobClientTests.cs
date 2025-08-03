using JobSharp.Core;
using JobSharp.Jobs;
using JobSharp.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace JobSharp.Tests;

public class JobClientTests
{
    private readonly IJobStorage _jobStorage;
    private readonly ILogger<JobClient> _logger;
    private readonly JobClient _client;

    public JobClientTests()
    {
        _jobStorage = Substitute.For<IJobStorage>();
        _logger = Substitute.For<ILogger<JobClient>>();
        _client = new JobClient(_jobStorage, _logger);
    }

    [Fact]
    public async Task EnqueueAsync_ShouldStoreJobAndReturnJobId()
    {
        // Arrange
        var args = new TestJobArgs { Value = "test" };
        _jobStorage.StoreJobAsync(Arg.Any<IJob>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<IJob>().Id));

        // Act
        var jobId = await _client.EnqueueAsync(args);

        // Assert
        jobId.ShouldNotBeNullOrEmpty();
        await _jobStorage.Received(1).StoreJobAsync(
            Arg.Any<IJob>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleAsync_WithDelay_ShouldStoreScheduledJobAndReturnJobId()
    {
        // Arrange
        var args = new TestJobArgs { Value = "test" };
        var delay = TimeSpan.FromMinutes(5);
        _jobStorage.StoreJobAsync(Arg.Any<IJob>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<IJob>().Id));

        // Act
        var jobId = await _client.ScheduleAsync(args, delay);

        // Assert
        jobId.ShouldNotBeNullOrEmpty();
        await _jobStorage.Received(1).StoreJobAsync(
            Arg.Any<IJob>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleAsync_WithSpecificTime_ShouldStoreScheduledJobAndReturnJobId()
    {
        // Arrange
        var args = new TestJobArgs { Value = "test" };
        var scheduledAt = DateTimeOffset.UtcNow.AddHours(2);
        _jobStorage.StoreJobAsync(Arg.Any<IJob>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<IJob>().Id));

        // Act
        var jobId = await _client.ScheduleAsync(args, scheduledAt);

        // Assert
        jobId.ShouldNotBeNullOrEmpty();
        await _jobStorage.Received(1).StoreJobAsync(
            Arg.Any<IJob>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetJobAsync_ShouldReturnJobFromStorage()
    {
        // Arrange
        var jobId = "test-job-id";
        var expectedJob = new Job
        {
            Id = jobId,
            TypeName = typeof(TestJobArgs).Name,
            State = JobState.Created
        };
        _jobStorage.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(expectedJob);

        // Act
        var result = await _client.GetJobAsync(jobId);

        // Assert
        result.ShouldBe(expectedJob);
        await _jobStorage.Received(1).GetJobAsync(jobId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteJobAsync_ShouldCallStorageDeleteJob()
    {
        // Arrange
        var jobId = "test-job-id";

        // Act
        await _client.DeleteJobAsync(jobId);

        // Assert
        await _jobStorage.Received(1).DeleteJobAsync(jobId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetJobCountAsync_ShouldReturnCountFromStorage()
    {
        // Arrange
        var expectedCount = 42;
        var state = JobState.Created;
        _jobStorage.GetJobCountAsync(state, Arg.Any<CancellationToken>())
            .Returns(expectedCount);

        // Act
        var result = await _client.GetJobCountAsync(state);

        // Assert
        result.ShouldBe(expectedCount);
        await _jobStorage.Received(1).GetJobCountAsync(state, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueBatchAsync_ShouldStoreBatchAndReturnBatchIdAndJobIds()
    {
        // Arrange
        var argumentsList = new List<TestJobArgs>
        {
            new() { Value = "test1" },
            new() { Value = "test2" }
        };

        _jobStorage.StoreBatchAsync(Arg.Any<string>(), Arg.Any<IEnumerable<IJob>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _client.EnqueueBatchAsync(argumentsList);

        // Assert
        result.BatchId.ShouldNotBeNullOrEmpty();
        result.JobIds.Count().ShouldBe(2);
        await _jobStorage.Received(1).StoreBatchAsync(
            result.BatchId,
            Arg.Is<IEnumerable<IJob>>(jobs => jobs.Count() == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContinueWithAsync_ShouldStoreContinuationJobAndReturnJobId()
    {
        // Arrange
        var parentJobId = "parent-job-id";
        var args = new TestJobArgs { Value = "continuation" };
        _jobStorage.StoreContinuationAsync(Arg.Any<string>(), Arg.Any<IJob>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var continuationJobId = await _client.ContinueWithAsync(parentJobId, args);

        // Assert
        continuationJobId.ShouldNotBeNullOrEmpty();
        await _jobStorage.Received(1).StoreContinuationAsync(
            parentJobId,
            Arg.Any<IJob>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddOrUpdateRecurringJobAsync_ShouldStoreRecurringJob()
    {
        // Arrange
        var recurringJobId = "recurring-job-id";
        var args = new TestJobArgs { Value = "recurring" };
        var cronExpression = "0 */5 * * *";

        _jobStorage.StoreRecurringJobAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IJob>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _client.AddOrUpdateRecurringJobAsync(recurringJobId, args, cronExpression);

        // Assert
        await _jobStorage.Received(1).StoreRecurringJobAsync(
            recurringJobId,
            cronExpression,
            Arg.Any<IJob>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveRecurringJobAsync_ShouldCallStorageRemoveRecurringJob()
    {
        // Arrange
        var recurringJobId = "recurring-job-id";

        // Act
        await _client.RemoveRecurringJobAsync(recurringJobId);

        // Assert
        await _jobStorage.Received(1).RemoveRecurringJobAsync(recurringJobId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_WithNullJobStorage_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new JobClient(null!, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new JobClient(_jobStorage, null!));
    }
}

public class TestJobArgs
{
    public string Value { get; set; } = "";
}
