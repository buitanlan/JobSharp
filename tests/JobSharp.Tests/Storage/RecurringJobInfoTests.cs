using JobSharp.Core;
using JobSharp.Jobs;
using JobSharp.Storage;
using Shouldly;
using Xunit;

namespace JobSharp.Tests.Storage;

public class RecurringJobInfoTests
{
    [Fact]
    public void RecurringJobInfo_ShouldInitializeWithRequiredProperties()
    {
        // Arrange
        var id = "recurring-job-1";
        var cronExpression = "0 */5 * * * *";
        var jobTemplate = new Job
        {
            Id = "template-job",
            TypeName = "TestJob"
        };

        // Act
        var recurringJobInfo = new RecurringJobInfo
        {
            Id = id,
            CronExpression = cronExpression,
            JobTemplate = jobTemplate
        };

        // Assert
        recurringJobInfo.Id.ShouldBe(id);
        recurringJobInfo.CronExpression.ShouldBe(cronExpression);
        recurringJobInfo.JobTemplate.ShouldBe(jobTemplate);
        recurringJobInfo.IsEnabled.ShouldBeTrue(); // Default value
        recurringJobInfo.NextExecution.ShouldBeNull();
        recurringJobInfo.LastExecution.ShouldBeNull();
        recurringJobInfo.CreatedAt.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void RecurringJobInfo_ShouldAllowSettingAllProperties()
    {
        // Arrange
        var id = "recurring-job-1";
        var cronExpression = "0 0 12 * * *";
        var jobTemplate = new Job
        {
            Id = "template-job",
            TypeName = "TestJob",
            Arguments = "{\"value\": 42}"
        };
        var nextExecution = DateTimeOffset.UtcNow.AddHours(1);
        var lastExecution = DateTimeOffset.UtcNow.AddHours(-1);
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);

        // Act
        var recurringJobInfo = new RecurringJobInfo
        {
            Id = id,
            CronExpression = cronExpression,
            JobTemplate = jobTemplate,
            NextExecution = nextExecution,
            LastExecution = lastExecution,
            IsEnabled = false,
            CreatedAt = createdAt
        };

        // Assert
        recurringJobInfo.Id.ShouldBe(id);
        recurringJobInfo.CronExpression.ShouldBe(cronExpression);
        recurringJobInfo.JobTemplate.ShouldBe(jobTemplate);
        recurringJobInfo.NextExecution.ShouldBe(nextExecution);
        recurringJobInfo.LastExecution.ShouldBe(lastExecution);
        recurringJobInfo.IsEnabled.ShouldBeFalse();
        recurringJobInfo.CreatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public void RecurringJobInfo_WithComplexJobTemplate_ShouldWork()
    {
        // Arrange
        var jobTemplate = new Job
        {
            Id = Guid.NewGuid().ToString(),
            TypeName = "MyApp.Jobs.ComplexJob",
            Arguments = "{\"email\":\"test@example.com\",\"count\":5}",
            MaxRetryCount = 3,
            State = JobState.Created
        };

        // Act
        var recurringJobInfo = new RecurringJobInfo
        {
            Id = "complex-recurring-job",
            CronExpression = "0 0 9 * * MON-FRI", // Weekdays at 9 AM
            JobTemplate = jobTemplate
        };

        // Assert
        recurringJobInfo.JobTemplate.TypeName.ShouldBe("MyApp.Jobs.ComplexJob");
        recurringJobInfo.JobTemplate.Arguments.ShouldBe("{\"email\":\"test@example.com\",\"count\":5}");
        recurringJobInfo.JobTemplate.MaxRetryCount.ShouldBe(3);
        recurringJobInfo.CronExpression.ShouldBe("0 0 9 * * MON-FRI");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecurringJobInfo_IsEnabled_ShouldAcceptBooleanValues(bool isEnabled)
    {
        // Act
        var recurringJobInfo = new RecurringJobInfo
        {
            Id = "test-job",
            CronExpression = "* * * * * *",
            JobTemplate = new Job { Id = "test", TypeName = "Test" },
            IsEnabled = isEnabled
        };

        // Assert
        recurringJobInfo.IsEnabled.ShouldBe(isEnabled);
    }

    [Fact]
    public void RecurringJobInfo_WithNullableProperties_ShouldAllowNull()
    {
        // Act
        var recurringJobInfo = new RecurringJobInfo
        {
            Id = "test-job",
            CronExpression = "* * * * * *",
            JobTemplate = new Job { Id = "test", TypeName = "Test" },
            NextExecution = null,
            LastExecution = null
        };

        // Assert
        recurringJobInfo.NextExecution.ShouldBeNull();
        recurringJobInfo.LastExecution.ShouldBeNull();
    }
}