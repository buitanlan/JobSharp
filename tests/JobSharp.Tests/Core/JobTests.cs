using JobSharp.Core;
using JobSharp.Jobs;
using Shouldly;
using Xunit;

namespace JobSharp.Tests.Core;

public class JobTests
{
    [Fact]
    public void Job_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var job = new Job
        {
            Id = "test-id",
            TypeName = "TestJob"
        };

        // Assert
        job.Id.ShouldBe("test-id");
        job.TypeName.ShouldBe("TestJob");
        job.State.ShouldBe(JobState.Created);
        job.RetryCount.ShouldBe(0);
        job.MaxRetryCount.ShouldBe(3);
        job.CreatedAt.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        job.Arguments.ShouldBeNull();
        job.ErrorMessage.ShouldBeNull();
        job.Result.ShouldBeNull();
        job.ScheduledAt.ShouldBeNull();
        job.ExecutedAt.ShouldBeNull();
        job.BatchId.ShouldBeNull();
        job.ParentJobId.ShouldBeNull();
    }

    [Fact]
    public void Job_ShouldAllowSettingAllProperties()
    {
        // Arrange
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(30);
        var executedAt = DateTimeOffset.UtcNow;

        // Act
        var job = new Job
        {
            Id = "job-123",
            TypeName = "MyJob",
            Arguments = "{\"value\": 42}",
            State = JobState.Scheduled,
            CreatedAt = createdAt,
            ScheduledAt = scheduledAt,
            ExecutedAt = executedAt,
            RetryCount = 2,
            MaxRetryCount = 5,
            ErrorMessage = "Test error",
            Result = "Test result",
            BatchId = "batch-123",
            ParentJobId = "parent-123"
        };

        // Assert
        job.Id.ShouldBe("job-123");
        job.TypeName.ShouldBe("MyJob");
        job.Arguments.ShouldBe("{\"value\": 42}");
        job.State.ShouldBe(JobState.Scheduled);
        job.CreatedAt.ShouldBe(createdAt);
        job.ScheduledAt.ShouldBe(scheduledAt);
        job.ExecutedAt.ShouldBe(executedAt);
        job.RetryCount.ShouldBe(2);
        job.MaxRetryCount.ShouldBe(5);
        job.ErrorMessage.ShouldBe("Test error");
        job.Result.ShouldBe("Test result");
        job.BatchId.ShouldBe("batch-123");
        job.ParentJobId.ShouldBe("parent-123");
    }

    [Theory]
    [InlineData(JobState.Created)]
    [InlineData(JobState.Scheduled)]
    [InlineData(JobState.Processing)]
    [InlineData(JobState.Succeeded)]
    [InlineData(JobState.Failed)]
    [InlineData(JobState.Cancelled)]
    [InlineData(JobState.Abandoned)]
    [InlineData(JobState.AwaitingContinuation)]
    [InlineData(JobState.AwaitingBatch)]
    public void Job_ShouldAcceptAllJobStates(JobState state)
    {
        // Arrange & Act
        var job = new Job
        {
            Id = "test-id",
            TypeName = "TestJob",
            State = state
        };

        // Assert
        job.State.ShouldBe(state);
    }
}