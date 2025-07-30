namespace JobSharp.Core;

/// <summary>
/// Represents the possible states of a job in the JobSharp system.
/// </summary>
public enum JobState
{
    /// <summary>
    /// The job has been created but not yet scheduled for execution.
    /// </summary>
    Created = 0,

    /// <summary>
    /// The job is scheduled and waiting to be executed.
    /// </summary>
    Scheduled = 1,

    /// <summary>
    /// The job is currently being executed.
    /// </summary>
    Processing = 2,

    /// <summary>
    /// The job has completed successfully.
    /// </summary>
    Succeeded = 3,

    /// <summary>
    /// The job failed during execution but may be retried.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// The job was cancelled before completion.
    /// </summary>
    Cancelled = 5,

    /// <summary>
    /// The job failed and exceeded the maximum retry count.
    /// </summary>
    Abandoned = 6,

    /// <summary>
    /// The job is waiting for its continuation to be triggered.
    /// </summary>
    AwaitingContinuation = 7,

    /// <summary>
    /// The job is part of a batch that is not yet complete.
    /// </summary>
    AwaitingBatch = 8
}