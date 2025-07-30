namespace JobSharp.Dapper.Models;

/// <summary>
/// Represents a recurring job record for Dapper mapping.
/// </summary>
public class RecurringJobModel
{
    public required string Id { get; set; }
    public required string CronExpression { get; set; }
    public required string JobTypeName { get; set; }
    public string? JobArguments { get; set; }
    public int MaxRetryCount { get; set; }
    public DateTimeOffset? NextExecution { get; set; }
    public DateTimeOffset? LastExecution { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}