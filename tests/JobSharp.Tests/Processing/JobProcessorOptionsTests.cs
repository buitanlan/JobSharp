using JobSharp.Processing;
using Shouldly;
using Xunit;

namespace JobSharp.Tests.Processing;

public class JobProcessorOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeSet()
    {
        // Act
        var options = new JobProcessorOptions();

        // Assert
        options.MaxConcurrentJobs.ShouldBe(10);
        options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
        options.BatchSize.ShouldBe(100);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var maxConcurrentJobs = 10;
        var pollingInterval = TimeSpan.FromSeconds(30);
        var batchSize = 50;

        // Act
        var options = new JobProcessorOptions
        {
            MaxConcurrentJobs = maxConcurrentJobs,
            PollingInterval = pollingInterval,
            BatchSize = batchSize
        };

        // Assert
        options.MaxConcurrentJobs.ShouldBe(maxConcurrentJobs);
        options.PollingInterval.ShouldBe(pollingInterval);
        options.BatchSize.ShouldBe(batchSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxConcurrentJobs_WithInvalidValue_ShouldAllowButNotRecommended(int value)
    {
        // Act
        var options = new JobProcessorOptions
        {
            MaxConcurrentJobs = value
        };

        // Assert
        options.MaxConcurrentJobs.ShouldBe(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BatchSize_WithInvalidValue_ShouldAllowButNotRecommended(int value)
    {
        // Act
        var options = new JobProcessorOptions
        {
            BatchSize = value
        };

        // Assert
        options.BatchSize.ShouldBe(value);
    }

    [Fact]
    public void PollingInterval_WithZero_ShouldAllowButNotRecommended()
    {
        // Act
        var options = new JobProcessorOptions
        {
            PollingInterval = TimeSpan.Zero
        };

        // Assert
        options.PollingInterval.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void PollingInterval_WithNegative_ShouldAllowButNotRecommended()
    {
        // Act
        var options = new JobProcessorOptions
        {
            PollingInterval = TimeSpan.FromSeconds(-1)
        };

        // Assert
        options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(-1));
    }
}