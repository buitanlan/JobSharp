using JobSharp.Core;
using JobSharp.Example.Jobs;
using Microsoft.Extensions.Logging;

namespace JobSharp.Example.Handlers;

/// <summary>
/// Example job handler for sending emails.
/// </summary>
public class SendEmailJobHandler : JobHandlerBase<SendEmailJob>
{
    private readonly ILogger<SendEmailJobHandler> _logger;

    public SendEmailJobHandler(ILogger<SendEmailJobHandler> logger)
    {
        _logger = logger;
    }

    public override async Task<JobExecutionResult> HandleAsync(SendEmailJob job, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending email to {To} with subject '{Subject}'", job.To, job.Subject);

        try
        {
            // Simulate email sending
            await Task.Delay(Random.Shared.Next(1000, 3000), cancellationToken);

            // Simulate occasional failures for demonstration
            if (Random.Shared.Next(1, 10) <= 2) // 20% failure rate
            {
                throw new InvalidOperationException("SMTP server temporarily unavailable");
            }

            _logger.LogInformation("Email sent successfully to {To}", job.To);
            return JobExecutionResult.Success(new { EmailId = Guid.NewGuid().ToString(), SentAt = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", job.To);
            return JobExecutionResult.Failure(ex, shouldRetry: true, retryDelay: TimeSpan.FromSeconds(30));
        }
    }
}

/// <summary>
/// Example job handler for data processing.
/// </summary>
public class ProcessDataJobHandler : JobHandlerBase<ProcessDataJob>
{
    private readonly ILogger<ProcessDataJobHandler> _logger;

    public ProcessDataJobHandler(ILogger<ProcessDataJobHandler> logger)
    {
        _logger = logger;
    }

    public override async Task<JobExecutionResult> HandleAsync(ProcessDataJob job, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing data {DataId} with type {ProcessingType}", job.DataId, job.ProcessingType);

        try
        {
            // Simulate data processing
            var processingTime = job.ProcessingType.ToLower() switch
            {
                "fast" => Random.Shared.Next(500, 1000),
                "medium" => Random.Shared.Next(2000, 4000),
                "slow" => Random.Shared.Next(5000, 8000),
                _ => Random.Shared.Next(1000, 3000)
            };

            await Task.Delay(processingTime, cancellationToken);

            _logger.LogInformation("Data processing completed for {DataId}", job.DataId);
            return JobExecutionResult.Success(new
            {
                ProcessedDataId = job.DataId,
                ProcessingTime = processingTime,
                CompletedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process data {DataId}", job.DataId);
            return JobExecutionResult.Failure(ex);
        }
    }
}

/// <summary>
/// Example job handler for report generation.
/// </summary>
public class GenerateReportJobHandler : JobHandlerBase<GenerateReportJob>
{
    private readonly ILogger<GenerateReportJobHandler> _logger;

    public GenerateReportJobHandler(ILogger<GenerateReportJobHandler> logger)
    {
        _logger = logger;
    }

    public override async Task<JobExecutionResult> HandleAsync(GenerateReportJob job, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating {ReportType} report from {StartDate} to {EndDate}",
            job.ReportType, job.StartDate, job.EndDate);

        try
        {
            // Simulate report generation
            await Task.Delay(Random.Shared.Next(3000, 6000), cancellationToken);

            var reportId = Guid.NewGuid().ToString();
            _logger.LogInformation("Report {ReportId} generated successfully", reportId);

            return JobExecutionResult.Success(new
            {
                ReportId = reportId,
                ReportType = job.ReportType,
                FilePath = $"/reports/{reportId}.pdf",
                GeneratedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate {ReportType} report", job.ReportType);
            return JobExecutionResult.Failure(ex);
        }
    }
}

/// <summary>
/// Example job handler for cleanup tasks.
/// </summary>
public class CleanupJobHandler : JobHandlerBase<CleanupJob>
{
    private readonly ILogger<CleanupJobHandler> _logger;

    public CleanupJobHandler(ILogger<CleanupJobHandler> logger)
    {
        _logger = logger;
    }

    public override async Task<JobExecutionResult> HandleAsync(CleanupJob job, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting cleanup of {ResourceType} resources older than {MaxAge}",
            job.ResourceType, job.MaxAge);

        try
        {
            // Simulate cleanup operation
            await Task.Delay(Random.Shared.Next(2000, 4000), cancellationToken);

            var cleanedCount = Random.Shared.Next(10, 100);
            _logger.LogInformation("Cleanup completed. Removed {CleanedCount} {ResourceType} resources",
                cleanedCount, job.ResourceType);

            return JobExecutionResult.Success(new
            {
                ResourceType = job.ResourceType,
                CleanedCount = cleanedCount,
                CompletedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup {ResourceType} resources", job.ResourceType);
            return JobExecutionResult.Failure(ex);
        }
    }
}

/// <summary>
/// Example job handler for sending notifications.
/// </summary>
public class SendNotificationJobHandler : JobHandlerBase<SendNotificationJob>
{
    private readonly ILogger<SendNotificationJobHandler> _logger;

    public SendNotificationJobHandler(ILogger<SendNotificationJobHandler> logger)
    {
        _logger = logger;
    }

    public override async Task<JobExecutionResult> HandleAsync(SendNotificationJob job, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending {NotificationType} notification to user {UserId}: {Title}",
            job.NotificationType, job.UserId, job.Title);

        try
        {
            // Simulate notification sending
            await Task.Delay(Random.Shared.Next(500, 1500), cancellationToken);

            _logger.LogInformation("Notification sent successfully to user {UserId}", job.UserId);
            return JobExecutionResult.Success(new
            {
                NotificationId = Guid.NewGuid().ToString(),
                SentAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to user {UserId}", job.UserId);
            return JobExecutionResult.Failure(ex);
        }
    }
}