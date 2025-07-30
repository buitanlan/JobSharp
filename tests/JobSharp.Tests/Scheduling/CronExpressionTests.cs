using JobSharp.Scheduling;
using Shouldly;
using Xunit;

namespace JobSharp.Tests.Scheduling;

public class CronExpressionTests
{
    [Fact]
    public void Parse_WithValidCronExpression_ShouldReturnCronExpression()
    {
        // Arrange
        var cronExpression = "0 */5 * * *"; // Every 5 minutes

        // Act
        var result = CronExpression.Parse(cronExpression);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_WithInvalidCronExpression_ShouldThrowArgumentException()
    {
        // Arrange
        var invalidCronExpression = "invalid cron";

        // Act & Assert
        Should.Throw<ArgumentException>(() => CronExpression.Parse(invalidCronExpression));
    }

    [Fact]
    public void Parse_WithEmptyString_ShouldThrowArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => CronExpression.Parse(""));
    }

    [Fact]
    public void Parse_WithNull_ShouldThrowArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => CronExpression.Parse(null!));
    }

    [Fact]
    public void Parse_WithWrongNumberOfFields_ShouldThrowArgumentException()
    {
        // Arrange
        var cronExpression = "0 */5 *"; // Only 3 fields instead of 5

        // Act & Assert
        Should.Throw<ArgumentException>(() => CronExpression.Parse(cronExpression));
    }

    [Fact]
    public void GetNextOccurrence_WithEveryMinuteCron_ShouldReturnNextMinute()
    {
        // Arrange
        var cronExpression = CronExpression.Parse("* * * * *"); // Every minute
        var currentTime = new DateTime(2024, 1, 1, 10, 30, 0);

        // Act
        var nextOccurrence = cronExpression.GetNextOccurrence(currentTime);

        // Assert
        nextOccurrence.ShouldBe(new DateTime(2024, 1, 1, 10, 31, 0));
    }

    [Fact]
    public void GetNextOccurrence_WithEveryHourCron_ShouldReturnNextHour()
    {
        // Arrange
        var cronExpression = CronExpression.Parse("0 * * * *"); // Every hour at minute 0
        var currentTime = new DateTime(2024, 1, 1, 10, 30, 0);

        // Act
        var nextOccurrence = cronExpression.GetNextOccurrence(currentTime);

        // Assert
        nextOccurrence.ShouldBe(new DateTime(2024, 1, 1, 11, 0, 0));
    }

    [Fact]
    public void GetNextOccurrence_WithDailyCron_ShouldReturnNextDay()
    {
        // Arrange
        var cronExpression = CronExpression.Parse("0 12 * * *"); // Daily at noon
        var currentTime = new DateTime(2024, 1, 1, 10, 30, 0);

        // Act
        var nextOccurrence = cronExpression.GetNextOccurrence(currentTime);

        // Assert
        nextOccurrence.ShouldBe(new DateTime(2024, 1, 1, 12, 0, 0));
    }

    [Fact]
    public void GetNextOccurrence_WithDailyCronAfterTime_ShouldReturnNextDayOccurrence()
    {
        // Arrange
        var cronExpression = CronExpression.Parse("0 12 * * *"); // Daily at noon
        var currentTime = new DateTime(2024, 1, 1, 15, 30, 0); // After noon

        // Act
        var nextOccurrence = cronExpression.GetNextOccurrence(currentTime);

        // Assert
        nextOccurrence.ShouldBe(new DateTime(2024, 1, 2, 12, 0, 0));
    }

    [Fact]
    public void Parse_WithValidComplexExpression_ShouldWork()
    {
        // Arrange
        var cronExpression = "15 10 * * 1-5"; // 10:15 AM on weekdays

        // Act
        var result = CronExpression.Parse(cronExpression);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_WithRangeExpression_ShouldWork()
    {
        // Arrange
        var cronExpression = "0 9-17 * * *"; // Every hour from 9 AM to 5 PM

        // Act
        var result = CronExpression.Parse(cronExpression);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_WithListExpression_ShouldWork()
    {
        // Arrange
        var cronExpression = "0 0 1,15 * *"; // 1st and 15th of every month at midnight

        // Act
        var result = CronExpression.Parse(cronExpression);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_WithStepExpression_ShouldWork()
    {
        // Arrange
        var cronExpression = "0 */2 * * *"; // Every 2 hours

        // Act
        var result = CronExpression.Parse(cronExpression);

        // Assert
        result.ShouldNotBeNull();
    }
}