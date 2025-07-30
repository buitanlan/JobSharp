using System.Text.Json.Serialization;

namespace JobSharp.Redis.Models;

/// <summary>
/// Represents recurring job data for Redis JSON serialization.
/// </summary>
public class RecurringJobData
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("cronExpression")]
    public required string CronExpression { get; set; }

    [JsonPropertyName("jobTypeName")]
    public required string JobTypeName { get; set; }

    [JsonPropertyName("jobArguments")]
    public string? JobArguments { get; set; }

    [JsonPropertyName("maxRetryCount")]
    public int MaxRetryCount { get; set; }

    [JsonPropertyName("nextExecution")]
    public DateTimeOffset? NextExecution { get; set; }

    [JsonPropertyName("lastExecution")]
    public DateTimeOffset? LastExecution { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}