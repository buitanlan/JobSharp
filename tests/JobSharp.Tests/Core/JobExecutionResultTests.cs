using JobSharp.Core;
using Shouldly;
using Xunit;

namespace JobSharp.Tests.Core;

public class JobExecutionResultTests
{
    [Fact]
    public void Success_WithoutData_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = JobExecutionResult.Success();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Result.ShouldBeNull();
        result.ErrorMessage.ShouldBeNull();
        result.ShouldRetry.ShouldBeTrue();
        result.RetryDelay.ShouldBeNull();
    }

    [Fact]
    public void Success_WithData_ShouldCreateSuccessfulResultWithData()
    {
        // Arrange
        var data = "Test result data";

        // Act
        var result = JobExecutionResult.Success(data);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Result.ShouldBe(data);
        result.ErrorMessage.ShouldBeNull();
        result.ShouldRetry.ShouldBeTrue();
        result.RetryDelay.ShouldBeNull();
    }

    [Fact]
    public void Failure_WithErrorMessage_ShouldCreateFailedResult()
    {
        // Arrange
        var errorMessage = "Test error message";

        // Act
        var result = JobExecutionResult.Failure(errorMessage);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(errorMessage);
        result.Result.ShouldBeNull();
        result.ShouldRetry.ShouldBeTrue();
        result.RetryDelay.ShouldBeNull();
    }

    [Fact]
    public void Failure_WithErrorMessageAndShouldRetryFalse_ShouldCreateNonRetryableFailedResult()
    {
        // Arrange
        var errorMessage = "Test error message";

        // Act
        var result = JobExecutionResult.Failure(errorMessage, shouldRetry: false);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(errorMessage);
        result.Result.ShouldBeNull();
        result.ShouldRetry.ShouldBeFalse();
        result.RetryDelay.ShouldBeNull();
    }

    [Fact]
    public void Failure_WithRetryDelay_ShouldCreateFailedResultWithDelay()
    {
        // Arrange
        var errorMessage = "Test error message";
        var retryDelay = TimeSpan.FromMinutes(5);

        // Act
        var result = JobExecutionResult.Failure(errorMessage, retryDelay: retryDelay);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(errorMessage);
        result.Result.ShouldBeNull();
        result.ShouldRetry.ShouldBeTrue();
        result.RetryDelay.ShouldBe(retryDelay);
    }

    [Fact]
    public void Failure_WithException_ShouldCreateFailedResultFromException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act
        var result = JobExecutionResult.Failure(exception);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(exception.ToString());
        result.Result.ShouldBeNull();
        result.ShouldRetry.ShouldBeTrue();
        result.RetryDelay.ShouldBeNull();
    }

    [Fact]
    public void Failure_WithExceptionAndShouldRetryFalse_ShouldCreateNonRetryableFailedResult()
    {
        // Arrange
        var exception = new ArgumentException("Test argument exception");

        // Act
        var result = JobExecutionResult.Failure(exception, shouldRetry: false);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(exception.ToString());
        result.Result.ShouldBeNull();
        result.ShouldRetry.ShouldBeFalse();
        result.RetryDelay.ShouldBeNull();
    }

    [Fact]
    public void Failure_WithExceptionAndRetryDelay_ShouldCreateFailedResultWithDelay()
    {
        // Arrange
        var exception = new TimeoutException("Test timeout exception");
        var retryDelay = TimeSpan.FromSeconds(30);

        // Act
        var result = JobExecutionResult.Failure(exception, retryDelay: retryDelay);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(exception.ToString());
        result.Result.ShouldBeNull();
        result.ShouldRetry.ShouldBeTrue();
        result.RetryDelay.ShouldBe(retryDelay);
    }
}