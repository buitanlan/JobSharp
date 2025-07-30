using JobSharp.Core;
using Cassandra.Mapping.Attributes;

namespace JobSharp.Cassandra.Models;

/// <summary>
/// Represents a job row for Cassandra storage.
/// </summary>
[Table("jobs")]
public class JobRow
{
    [Column("id")]
    [PartitionKey]
    public string Id { get; set; } = null!;

    [Column("type_name")]
    public string TypeName { get; set; } = null!;

    [Column("arguments")]
    public string? Arguments { get; set; }

    [Column("state")]
    public int State { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("scheduled_at")]
    public DateTimeOffset? ScheduledAt { get; set; }

    [Column("executed_at")]
    public DateTimeOffset? ExecutedAt { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Column("max_retry_count")]
    public int MaxRetryCount { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("result")]
    public string? Result { get; set; }

    [Column("batch_id")]
    public string? BatchId { get; set; }

    [Column("parent_job_id")]
    public string? ParentJobId { get; set; }
}