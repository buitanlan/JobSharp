using Cassandra.Mapping.Attributes;

namespace JobSharp.Cassandra.Models;

/// <summary>
/// Represents a jobs by state row for efficient state-based queries in Cassandra.
/// </summary>
[Table("jobs_by_state")]
public class JobsByStateRow
{
    [Column("state")]
    [PartitionKey]
    public int State { get; set; }

    [Column("created_at")]
    [ClusteringKey(0)]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("job_id")]
    [ClusteringKey(1)]
    public string JobId { get; set; } = null!;
}