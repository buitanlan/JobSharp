namespace JobSharp.Example.Jobs;

/// <summary>
/// Example job arguments for sending an email.
/// </summary>
public class SendEmailJob
{
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public string? From { get; set; }
}

/// <summary>
/// Example job arguments for processing data.
/// </summary>
public class ProcessDataJob
{
    public required string DataId { get; set; }
    public required string ProcessingType { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Example job arguments for generating reports.
/// </summary>
public class GenerateReportJob
{
    public required string ReportType { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string? UserId { get; set; }
    public string[]? Filters { get; set; }
}

/// <summary>
/// Example job arguments for cleanup tasks.
/// </summary>
public class CleanupJob
{
    public required string ResourceType { get; set; }
    public TimeSpan MaxAge { get; set; }
    public int? BatchSize { get; set; }
}

/// <summary>
/// Example job arguments for sending notifications.
/// </summary>
public class SendNotificationJob
{
    public required string UserId { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public string NotificationType { get; set; } = "Info";
    public Dictionary<string, string>? Metadata { get; set; }
}