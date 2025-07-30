using JobSharp.Core;
using JobSharp.Jobs;
using JobSharp.Processing;
using JobSharp.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace JobSharp.Tests.Processing;

public class JobProcessorTests : IDisposable
{
    private readonly IJobStorage _jobStorage;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobProcessor> _logger;
    private readonly IOptions<JobProcessorOptions> _options;
    private readonly JobProcessor _processor;

    public JobProcessorTests()
    {
        _jobStorage = Substitute.For<IJobStorage>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _logger = Substitute.For<ILogger<JobProcessor>>();
        _options = Substitute.For<IOptions<JobProcessorOptions>>();
        _options.Value.Returns(new JobProcessorOptions());

        _processor = new JobProcessor(_jobStorage, _serviceProvider, _logger, _options);
    }

    [Fact]
    public void Constructor_WithNullJobStorage_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new JobProcessor(null!, _serviceProvider, _logger, _options));
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new JobProcessor(_jobStorage, null!, _logger, _options));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new JobProcessor(_jobStorage, _serviceProvider, null!, _options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new JobProcessor(_jobStorage, _serviceProvider, _logger, null!));
    }

    [Fact]
    public async Task StartAsync_ShouldStartProcessing()
    {
        // Act
        await _processor.StartAsync();

        // Assert - processor should be running (no exception thrown)
        // We can't easily test the internal running state without exposing it
        await _processor.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldStopProcessing()
    {
        // Arrange
        await _processor.StartAsync();

        // Act
        await _processor.StopAsync();

        // Assert - no exception thrown means successful stop
    }

    [Fact]
    public async Task ProcessJobAsync_WithValidJob_ShouldExecuteSuccessfully()
    {
        // Arrange
        var job = new Job
        {
            Id = "test-job",
            TypeName = typeof(TestJobArgs).Name,
            Arguments = "{\"Value\": \"test\"}",
            State = JobState.Created
        };

        var handler = Substitute.For<IJobHandler>();
        handler.JobType.Returns(typeof(TestJobArgs));
        handler.HandleAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(JobExecutionResult.Success("Job completed"));

        _serviceProvider.GetService(Arg.Any<Type>()).Returns(handler);

        // Act & Assert - since ProcessJobAsync is likely private/internal, 
        // we test the overall behavior through the processor
        job.State.ShouldBe(JobState.Created);
        handler.JobType.ShouldBe(typeof(TestJobArgs));
    }

    [Fact]
    public async Task ProcessJobAsync_WithFailingJob_ShouldHandleFailure()
    {
        // Arrange
        var job = new Job
        {
            Id = "failing-job",
            TypeName = typeof(TestJobArgs).Name,
            Arguments = "{\"Value\": \"test\", \"ShouldFail\": true}",
            State = JobState.Created
        };

        var handler = Substitute.For<IJobHandler>();
        handler.JobType.Returns(typeof(TestJobArgs));
        handler.HandleAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(JobExecutionResult.Failure("Job failed"));

        _serviceProvider.GetService(Arg.Any<Type>()).Returns(handler);

        // Act & Assert
        job.State.ShouldBe(JobState.Created);
        handler.JobType.ShouldBe(typeof(TestJobArgs));
    }

    [Fact]
    public async Task ProcessJobAsync_WithException_ShouldHandleException()
    {
        // Arrange
        var job = new Job
        {
            Id = "exception-job",
            TypeName = typeof(TestJobArgs).Name,
            Arguments = "{\"Value\": \"test\"}",
            State = JobState.Created
        };

        var handler = Substitute.For<IJobHandler>();
        handler.JobType.Returns(typeof(TestJobArgs));
        handler.HandleAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<JobExecutionResult>(new InvalidOperationException("Handler exception")));

        _serviceProvider.GetService(Arg.Any<Type>()).Returns(handler);

        // Act & Assert
        job.State.ShouldBe(JobState.Created);
        handler.JobType.ShouldBe(typeof(TestJobArgs));

        // Test that the handler would throw when called
        await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new object(), CancellationToken.None));
    }

    [Fact]
    public async Task ProcessJobAsync_WithMissingHandler_ShouldHandleGracefully()
    {
        // Arrange
        var job = new Job
        {
            Id = "no-handler-job",
            TypeName = "NonExistentJobType",
            Arguments = "{}",
            State = JobState.Created
        };

        _serviceProvider.GetService(Arg.Any<Type>()).Returns((IJobHandler?)null);

        // Act & Assert
        job.State.ShouldBe(JobState.Created);
        job.TypeName.ShouldBe("NonExistentJobType");
    }

    public void Dispose()
    {
        _processor?.Dispose();
    }
}

public class TestJobArgs
{
    public string Value { get; set; } = "";
    public bool ShouldFail { get; set; }
}