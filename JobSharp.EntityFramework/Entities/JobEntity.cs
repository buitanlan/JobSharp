using JobSharp.Core;
using System.ComponentModel.DataAnnotations;

namespace JobSharp.EntityFramework.Entities;

/// <summary>
/// Entity Framework entity representing a job.
/// </summary>
public class JobEntity
{
    [Key]
    [MaxLength(36)]
    public required string Id { get; set; }

    [Required]
    [MaxLength(500)]
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

    [MaxLength(36)]
    public string? BatchId { get; set; }

    [MaxLength(36)]
    public string? ParentJobId { get; set; }

    // Navigation properties
    public virtual ICollection<JobEntity> Continuations { get; set; } = new List<JobEntity>();
    public virtual JobEntity? ParentJob { get; set; }
    public virtual ICollection<JobEntity> BatchJobs { get; set; } = new List<JobEntity>();
} 