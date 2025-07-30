namespace JobSharp.Processing;

/// <summary>
/// Configuration options for the job processor.
/// </summary>
public class JobProcessorOptions
{
    /// <summary>
    /// Gets or sets the maximum number of jobs that can be processed concurrently.
    /// Default is 10.
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 10;

    /// <summary>
    /// Gets or sets the interval at which the processor polls for scheduled jobs.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the interval at which the processor polls for recurring jobs.
    /// Default is 1 minute.
    /// </summary>
    public TimeSpan RecurringJobsPollingInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the number of jobs to retrieve in each batch when polling.
    /// Default is 100.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the default delay before retrying a failed job.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan DefaultRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the timeout for shutting down the processor and waiting for running jobs to complete.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to automatically delete completed jobs.
    /// Default is false.
    /// </summary>
    public bool AutoDeleteCompletedJobs { get; set; } = false;

    /// <summary>
    /// Gets or sets the age after which completed jobs are automatically deleted (if enabled).
    /// Default is 24 hours.
    /// </summary>
    public TimeSpan CompletedJobRetentionPeriod { get; set; } = TimeSpan.FromHours(24);
} 