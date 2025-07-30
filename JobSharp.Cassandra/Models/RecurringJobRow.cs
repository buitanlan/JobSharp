using Cassandra.Mapping.Attributes;

namespace JobSharp.Cassandra.Models;

/// <summary>
/// Represents a recurring job row for Cassandra storage.
/// </summary>
[Table("recurring_jobs")]
public class RecurringJobRow
{
    [Column("id")]
    [PartitionKey]
    public string Id { get; set; } = null!;

    [Column("cron_expression")]
    public string CronExpression { get; set; } = null!;

    [Column("job_type_name")]
    public string JobTypeName { get; set; } = null!;

    [Column("job_arguments")]
    public string? JobArguments { get; set; }

    [Column("max_retry_count")]
    public int MaxRetryCount { get; set; }

    [Column("next_execution")]
    public DateTimeOffset? NextExecution { get; set; }

    [Column("last_execution")]
    public DateTimeOffset? LastExecution { get; set; }

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}