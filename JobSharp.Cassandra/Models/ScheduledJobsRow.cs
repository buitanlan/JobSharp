using Cassandra.Mapping.Attributes;

namespace JobSharp.Cassandra.Models;

/// <summary>
/// Represents a scheduled jobs row for efficient scheduled job queries in Cassandra.
/// </summary>
[Table("scheduled_jobs")]
public class ScheduledJobsRow
{
    [Column("bucket")]
    [PartitionKey]
    public int Bucket { get; set; }

    [Column("scheduled_at")]
    [ClusteringKey(0)]
    public DateTimeOffset ScheduledAt { get; set; }

    [Column("job_id")]
    [ClusteringKey(1)]
    public string JobId { get; set; } = null!;
}