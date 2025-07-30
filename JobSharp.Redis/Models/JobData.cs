using JobSharp.Core;
using System.Text.Json.Serialization;

namespace JobSharp.Redis.Models;

/// <summary>
/// Represents job data for Redis JSON serialization.
/// </summary>
public class JobData
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("typeName")]
    public required string TypeName { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }

    [JsonPropertyName("state")]
    public JobState State { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("scheduledAt")]
    public DateTimeOffset? ScheduledAt { get; set; }

    [JsonPropertyName("executedAt")]
    public DateTimeOffset? ExecutedAt { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    [JsonPropertyName("maxRetryCount")]
    public int MaxRetryCount { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("batchId")]
    public string? BatchId { get; set; }

    [JsonPropertyName("parentJobId")]
    public string? ParentJobId { get; set; }
}