using JobSharp.Core;

namespace JobSharp.Storage;

/// <summary>
/// Contains information about a recurring job schedule.
/// </summary>
public class RecurringJobInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for the recurring job.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the cron expression defining the schedule.
    /// </summary>
    public required string CronExpression { get; init; }

    /// <summary>
    /// Gets or sets the job template used to create job instances.
    /// </summary>
    public required IJob JobTemplate { get; init; }

    /// <summary>
    /// Gets or sets the next scheduled execution time.
    /// </summary>
    public DateTimeOffset? NextExecution { get; set; }

    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    public DateTimeOffset? LastExecution { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the recurring job is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the date and time when the recurring job was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}