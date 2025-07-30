using JobSharp.Core;
using JobSharp.Jobs;
using JobSharp.Scheduling;
using JobSharp.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace JobSharp.Processing;

/// <summary>
/// Default implementation of the job processor.
/// </summary>
public class JobProcessor : IJobProcessor, IDisposable
{
    private readonly IJobStorage _jobStorage;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobProcessor> _logger;
    private readonly JobProcessorOptions _options;
    private readonly ConcurrentDictionary<Type, IJobHandler> _jobHandlers;
    private readonly Timer _scheduledJobsTimer;
    private readonly Timer _recurringJobsTimer;
    private readonly SemaphoreSlim _processingSemaphore;
    private volatile bool _isRunning;
    private volatile bool _disposed;

    public JobProcessor(
        IJobStorage jobStorage,
        IServiceProvider serviceProvider,
        ILogger<JobProcessor> logger,
        IOptions<JobProcessorOptions> options)
    {
        _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _jobHandlers = new ConcurrentDictionary<Type, IJobHandler>();
        _processingSemaphore = new SemaphoreSlim(_options.MaxConcurrentJobs);

        _scheduledJobsTimer = new Timer(ProcessScheduledJobs, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _recurringJobsTimer = new Timer(ProcessRecurringJobs, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        LoadJobHandlers();
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            return Task.CompletedTask;

        _logger.LogInformation("Starting job processor with {MaxConcurrentJobs} max concurrent jobs", _options.MaxConcurrentJobs);

        _isRunning = true;
        _scheduledJobsTimer.Change(TimeSpan.Zero, _options.PollingInterval);
        _recurringJobsTimer.Change(TimeSpan.Zero, _options.RecurringJobsPollingInterval);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
            return;

        _logger.LogInformation("Stopping job processor");

        _isRunning = false;
        _scheduledJobsTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _recurringJobsTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        // Wait for all running jobs to complete
        var timeout = _options.ShutdownTimeout;
        var waitStart = DateTime.UtcNow;

        while (_processingSemaphore.CurrentCount < _options.MaxConcurrentJobs &&
               DateTime.UtcNow - waitStart < timeout)
        {
            await Task.Delay(100, cancellationToken);
        }

        _logger.LogInformation("Job processor stopped");
    }

    public async Task ProcessJobAsync(IJob job, CancellationToken cancellationToken = default)
    {
        if (!_isRunning || _disposed)
            return;

        await _processingSemaphore.WaitAsync(cancellationToken);
        try
        {
            await ProcessJobInternalAsync(job, cancellationToken);
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    private async void ProcessScheduledJobs(object? state)
    {
        if (!_isRunning || _disposed)
            return;

        try
        {
            var jobs = await _jobStorage.GetScheduledJobsAsync(_options.BatchSize);
            var tasks = new List<Task>();

            foreach (var job in jobs)
            {
                if (!_isRunning)
                    break;

                if (job.ScheduledAt <= DateTimeOffset.UtcNow)
                {
                    tasks.Add(ProcessJobAsync(job));
                }
            }

            if (tasks.Any())
            {
                await Task.WhenAll(tasks);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scheduled jobs");
        }
    }

    private async void ProcessRecurringJobs(object? state)
    {
        if (!_isRunning || _disposed)
            return;

        try
        {
            var recurringJobs = await _jobStorage.GetRecurringJobsAsync();
            var now = DateTimeOffset.UtcNow;

            foreach (var recurringJob in recurringJobs)
            {
                if (!_isRunning)
                    break;

                if (!recurringJob.IsEnabled)
                    continue;

                try
                {
                    var cronExpression = CronExpression.Parse(recurringJob.CronExpression);
                    var lastExecution = recurringJob.LastExecution?.DateTime ?? DateTime.UtcNow.AddMinutes(-1);
                    var nextOccurrence = cronExpression.GetNextOccurrence(lastExecution);

                    if (nextOccurrence <= DateTime.UtcNow)
                    {
                        // Create a new job instance from the template
                        var jobTemplate = recurringJob.JobTemplate;
                        var newJob = new Job
                        {
                            Id = Guid.NewGuid().ToString(),
                            TypeName = jobTemplate.TypeName,
                            Arguments = jobTemplate.Arguments,
                            MaxRetryCount = jobTemplate.MaxRetryCount,
                            State = JobState.Scheduled,
                            ScheduledAt = now
                        };

                        await _jobStorage.StoreJobAsync(newJob);

                        // Update the recurring job's last execution time
                        recurringJob.LastExecution = now;
                        recurringJob.NextExecution = cronExpression.GetNextOccurrence(DateTime.UtcNow);

                        _logger.LogDebug("Created recurring job instance {JobId} from template {RecurringJobId}",
                            newJob.Id, recurringJob.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing recurring job {RecurringJobId}", recurringJob.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing recurring jobs");
        }
    }

    private async Task ProcessJobInternalAsync(IJob job, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing job {JobId} of type {JobType}", job.Id, job.TypeName);

        var mutableJob = job as Job ?? throw new InvalidOperationException("Job must be mutable for processing");

        try
        {
            mutableJob.State = JobState.Processing;
            mutableJob.ExecutedAt = DateTimeOffset.UtcNow;
            await _jobStorage.UpdateJobAsync(mutableJob, cancellationToken);

            var jobType = Type.GetType(job.TypeName);
            if (jobType == null)
            {
                throw new InvalidOperationException($"Job type '{job.TypeName}' not found");
            }

            if (!_jobHandlers.TryGetValue(jobType, out var handler))
            {
                throw new InvalidOperationException($"No handler found for job type '{jobType.Name}'");
            }

            var jobArguments = job.Arguments != null
                ? System.Text.Json.JsonSerializer.Deserialize(job.Arguments, jobType)
                : Activator.CreateInstance(jobType);

            var result = await handler.HandleAsync(jobArguments!, cancellationToken);

            if (result.IsSuccess)
            {
                mutableJob.State = JobState.Succeeded;
                mutableJob.SetResult(result.Result);
                await _jobStorage.UpdateJobAsync(mutableJob, cancellationToken);

                _logger.LogDebug("Job {JobId} completed successfully", job.Id);

                // Process continuations
                await ProcessContinuationsAsync(job.Id, cancellationToken);

                // Check batch completion
                if (!string.IsNullOrEmpty(mutableJob.BatchId))
                {
                    await CheckBatchCompletionAsync(mutableJob.BatchId, cancellationToken);
                }
            }
            else
            {
                await HandleJobFailureAsync(mutableJob, result, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", job.Id);
            await HandleJobFailureAsync(mutableJob, JobExecutionResult.Failure(ex), cancellationToken);
        }
    }

    private async Task HandleJobFailureAsync(Job job, JobExecutionResult result, CancellationToken cancellationToken)
    {
        job.ErrorMessage = result.ErrorMessage;
        job.RetryCount++;

        if (result.ShouldRetry && job.RetryCount <= job.MaxRetryCount)
        {
            job.State = JobState.Scheduled;
            job.ScheduledAt = DateTimeOffset.UtcNow.Add(result.RetryDelay ?? _options.DefaultRetryDelay);

            _logger.LogWarning("Job {JobId} failed, scheduling retry {RetryCount}/{MaxRetryCount}",
                job.Id, job.RetryCount, job.MaxRetryCount);
        }
        else
        {
            job.State = JobState.Abandoned;
            _logger.LogError("Job {JobId} abandoned after {RetryCount} attempts", job.Id, job.RetryCount);
        }

        await _jobStorage.UpdateJobAsync(job, cancellationToken);
    }

    private async Task ProcessContinuationsAsync(string parentJobId, CancellationToken cancellationToken)
    {
        var continuations = await _jobStorage.GetContinuationsAsync(parentJobId, cancellationToken);

        foreach (var continuation in continuations.Cast<Job>())
        {
            continuation.State = JobState.Scheduled;
            continuation.ScheduledAt = DateTimeOffset.UtcNow;
            await _jobStorage.UpdateJobAsync(continuation, cancellationToken);
        }
    }

    private async Task CheckBatchCompletionAsync(string batchId, CancellationToken cancellationToken)
    {
        var batchJobs = await _jobStorage.GetBatchJobsAsync(batchId, cancellationToken);
        var allCompleted = batchJobs.All(j => j.State is JobState.Succeeded or JobState.Abandoned);

        if (allCompleted)
        {
            // Find batch continuation jobs
            var batchContinuations = batchJobs
                .Where(j => j.State == JobState.AwaitingBatch)
                .Cast<Job>();

            foreach (var continuation in batchContinuations)
            {
                continuation.State = JobState.Scheduled;
                continuation.ScheduledAt = DateTimeOffset.UtcNow;
                await _jobStorage.UpdateJobAsync(continuation, cancellationToken);
            }
        }
    }

    private void LoadJobHandlers()
    {
        var handlerServices = _serviceProvider.GetServices<IJobHandler>();

        foreach (var handler in handlerServices)
        {
            _jobHandlers.TryAdd(handler.JobType, handler);
            _logger.LogDebug("Registered job handler for type {JobType}", handler.JobType.Name);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _scheduledJobsTimer?.Dispose();
        _recurringJobsTimer?.Dispose();
        _processingSemaphore?.Dispose();
    }
}