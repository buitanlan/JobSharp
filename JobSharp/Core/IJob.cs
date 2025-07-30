namespace JobSharp.Core;

/// <summary>
/// Defines the base interface for all jobs in the JobSharp system.
/// </summary>
public interface IJob
{
    /// <summary>
    /// Gets the unique identifier for this job.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the type name of the job for serialization purposes.
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// Gets the serialized arguments for the job.
    /// </summary>
    string? Arguments { get; }

    /// <summary>
    /// Gets the current state of the job.
    /// </summary>
    JobState State { get; }

    /// <summary>
    /// Gets the date and time when the job was created.
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the date and time when the job should be executed.
    /// For immediate jobs, this should be the creation time or earlier.
    /// </summary>
    DateTimeOffset? ScheduledAt { get; }

    /// <summary>
    /// Gets the date and time when the job was last executed.
    /// </summary>
    DateTimeOffset? ExecutedAt { get; }

    /// <summary>
    /// Gets the number of retry attempts made for this job.
    /// </summary>
    int RetryCount { get; }

    /// <summary>
    /// Gets the maximum number of retry attempts allowed for this job.
    /// </summary>
    int MaxRetryCount { get; }

    /// <summary>
    /// Gets the error message if the job failed.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Gets the result of the job execution if successful.
    /// </summary>
    string? Result { get; }
}