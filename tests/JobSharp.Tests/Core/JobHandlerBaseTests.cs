using JobSharp.Core;
using Shouldly;
using Xunit;

namespace JobSharp.Tests.Core;

// Test implementation of JobHandlerBase
public class TestJobHandler : JobHandlerBase<TestJobArgs>
{
    public override async Task<JobExecutionResult> HandleAsync(TestJobArgs args, CancellationToken cancellationToken)
    {
        if (args.ShouldFail)
            return JobExecutionResult.Failure("Test failure");

        if (args.ShouldThrow)
            throw new InvalidOperationException("Test exception");

        await Task.Delay(10, cancellationToken);
        return JobExecutionResult.Success($"Processed: {args.Value}");
    }
}

public class TestJobArgs
{
    public string Value { get; set; } = "";
    public bool ShouldFail { get; set; }
    public bool ShouldThrow { get; set; }
}

public class JobHandlerBaseTests
{
    private readonly TestJobHandler _handler;

    public JobHandlerBaseTests()
    {
        _handler = new TestJobHandler();
    }

    [Fact]
    public async Task HandleAsync_WithValidArgs_ShouldReturnSuccess()
    {
        // Arrange
        var args = new TestJobArgs { Value = "test-value" };

        // Act
        var result = await _handler.HandleAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Result.ShouldBe("Processed: test-value");
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithFailingJob_ShouldReturnFailure()
    {
        // Arrange
        var args = new TestJobArgs { Value = "test-value", ShouldFail = true };

        // Act
        var result = await _handler.HandleAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Test failure");
    }

    [Fact]
    public async Task HandleAsync_WithException_ShouldReturnFailure()
    {
        // Arrange
        var args = new TestJobArgs { Value = "test-value", ShouldThrow = true };

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(args, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_NonGeneric_WithValidObject_ShouldReturnSuccess()
    {
        // Arrange
        var args = new TestJobArgs { Value = "test-value" };

        // Act
        var result = await _handler.HandleAsync((object)args, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Result.ShouldBe("Processed: test-value");
    }

    [Fact]
    public async Task HandleAsync_NonGeneric_WithInvalidType_ShouldReturnFailure()
    {
        // Arrange
        var invalidArgs = "not-a-test-job-args";

        // Act
        var result = await _handler.HandleAsync(invalidArgs, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("Job is not of expected type TestJobArgs");
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var args = new TestJobArgs { Value = "test-value" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(args, cts.Token));
    }

    [Fact]
    public void JobType_ShouldReturnCorrectType()
    {
        // Act & Assert
        _handler.JobType.ShouldBe(typeof(TestJobArgs));
    }
}