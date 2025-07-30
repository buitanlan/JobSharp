using JobSharp.Core;
using System.Text.Json;

namespace JobSharp.Jobs;

/// <summary>
/// Base implementation of the IJob interface.
/// </summary>
public class Job : IJob
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string TypeName { get; init; }
    public string? Arguments { get; set; }
    public JobState State { get; set; } = JobState.Created;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public int RetryCount { get; set; } = 0;
    public int MaxRetryCount { get; set; } = 3;
    public string? ErrorMessage { get; set; }
    public string? Result { get; set; }

    /// <summary>
    /// Gets or sets the batch identifier if this job is part of a batch.
    /// </summary>
    public string? BatchId { get; set; }

    /// <summary>
    /// Gets or sets the parent job identifier if this job is a continuation.
    /// </summary>
    public string? ParentJobId { get; set; }

    /// <summary>
    /// Creates a new fire-and-forget job.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="arguments">The job arguments.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts.</param>
    /// <returns>A new job instance.</returns>
    public static Job CreateFireAndForget<T>(T arguments, int maxRetryCount = 3) where T : class
    {
        return new Job
        {
            TypeName = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name,
            Arguments = JsonSerializer.Serialize(arguments),
            ScheduledAt = DateTimeOffset.UtcNow,
            MaxRetryCount = maxRetryCount,
            State = JobState.Scheduled
        };
    }

    /// <summary>
    /// Creates a new delayed job.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="arguments">The job arguments.</param>
    /// <param name="delay">The delay before the job should be executed.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts.</param>
    /// <returns>A new job instance.</returns>
    public static Job CreateDelayed<T>(T arguments, TimeSpan delay, int maxRetryCount = 3) where T : class
    {
        return new Job
        {
            TypeName = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name,
            Arguments = JsonSerializer.Serialize(arguments),
            ScheduledAt = DateTimeOffset.UtcNow.Add(delay),
            MaxRetryCount = maxRetryCount,
            State = JobState.Scheduled
        };
    }

    /// <summary>
    /// Creates a new delayed job scheduled for a specific time.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="arguments">The job arguments.</param>
    /// <param name="scheduledAt">The time when the job should be executed.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts.</param>
    /// <returns>A new job instance.</returns>
    public static Job CreateScheduled<T>(T arguments, DateTimeOffset scheduledAt, int maxRetryCount = 3) where T : class
    {
        return new Job
        {
            TypeName = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name,
            Arguments = JsonSerializer.Serialize(arguments),
            ScheduledAt = scheduledAt,
            MaxRetryCount = maxRetryCount,
            State = JobState.Scheduled
        };
    }

    /// <summary>
    /// Creates a continuation job that will be executed after a parent job completes.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="parentJobId">The identifier of the parent job.</param>
    /// <param name="arguments">The job arguments.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts.</param>
    /// <returns>A new job instance.</returns>
    public static Job CreateContinuation<T>(string parentJobId, T arguments, int maxRetryCount = 3) where T : class
    {
        return new Job
        {
            TypeName = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name,
            Arguments = JsonSerializer.Serialize(arguments),
            ParentJobId = parentJobId,
            MaxRetryCount = maxRetryCount,
            State = JobState.AwaitingContinuation
        };
    }

    /// <summary>
    /// Creates a batch of jobs that will be executed together.
    /// </summary>
    /// <typeparam name="T">The type of job arguments.</typeparam>
    /// <param name="batchId">The unique identifier for the batch.</param>
    /// <param name="argumentsList">The list of job arguments for each job in the batch.</param>
    /// <param name="maxRetryCount">The maximum number of retry attempts per job.</param>
    /// <returns>A collection of job instances.</returns>
    public static IEnumerable<Job> CreateBatch<T>(string batchId, IEnumerable<T> argumentsList, int maxRetryCount = 3) where T : class
    {
        var typeName = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name;

        return argumentsList.Select(arguments => new Job
        {
            TypeName = typeName,
            Arguments = JsonSerializer.Serialize(arguments),
            BatchId = batchId,
            ScheduledAt = DateTimeOffset.UtcNow,
            MaxRetryCount = maxRetryCount,
            State = JobState.AwaitingBatch
        });
    }

    /// <summary>
    /// Deserializes the job arguments to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>The deserialized arguments.</returns>
    public T? GetArguments<T>() where T : class
    {
        if (string.IsNullOrEmpty(Arguments))
            return null;

        return JsonSerializer.Deserialize<T>(Arguments);
    }

    /// <summary>
    /// Sets the job result.
    /// </summary>
    /// <param name="result">The result to set.</param>
    public void SetResult(object? result)
    {
        Result = result != null ? JsonSerializer.Serialize(result) : null;
    }

    /// <summary>
    /// Gets the deserialized job result.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>The deserialized result.</returns>
    public T? GetResult<T>() where T : class
    {
        if (string.IsNullOrEmpty(Result))
            return null;

        return JsonSerializer.Deserialize<T>(Result);
    }
}