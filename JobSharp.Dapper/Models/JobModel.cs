using JobSharp.Core;

namespace JobSharp.Dapper.Models;

/// <summary>
/// Represents a job record for Dapper mapping.
/// </summary>
public class JobModel
{
    public required string Id { get; set; }
    public required string TypeName { get; set; }
    public string? Arguments { get; set; }
    public JobState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Result { get; set; }
    public string? BatchId { get; set; }
    public string? ParentJobId { get; set; }
}