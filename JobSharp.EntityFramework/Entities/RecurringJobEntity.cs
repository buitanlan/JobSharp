using System.ComponentModel.DataAnnotations;

namespace JobSharp.EntityFramework.Entities;

/// <summary>
/// Entity Framework entity representing a recurring job schedule.
/// </summary>
public class RecurringJobEntity
{
    [Key]
    [MaxLength(200)]
    public required string Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string CronExpression { get; set; }

    [Required]
    [MaxLength(500)]
    public required string JobTypeName { get; set; }

    public string? JobArguments { get; set; }

    public int MaxRetryCount { get; set; }

    public DateTimeOffset? NextExecution { get; set; }

    public DateTimeOffset? LastExecution { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}