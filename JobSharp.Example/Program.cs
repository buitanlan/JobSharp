using JobSharp;
using JobSharp.EntityFramework;
using JobSharp.EntityFramework.Extensions;
using JobSharp.Example.Handlers;
using JobSharp.Example.Jobs;
using JobSharp.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobSharp.Example;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 JobSharp Example Application");
        Console.WriteLine("================================");

        var host = CreateHostBuilder(args).Build();

        // Ensure database is created
        await host.Services.EnsureJobSharpDatabaseCreatedAsync();

        // Run the demonstration
        await RunDemonstrationAsync(host.Services);

        // Start the host (this will run the background job processor)
        Console.WriteLine("\n▶️  Starting job processor...");
        Console.WriteLine("Press Ctrl+C to stop the application");

        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Configure JobSharp with Entity Framework SQLite storage
                services.AddJobSharpEntityFrameworkSqlite("Data Source=jobsharp.db");

                // Configure JobSharp processor
                services.AddJobSharp(options =>
                {
                    options.MaxConcurrentJobs = 5;
                    options.PollingInterval = TimeSpan.FromSeconds(2);
                    options.RecurringJobsPollingInterval = TimeSpan.FromSeconds(30);
                });

                // Register job handlers
                services.AddJobHandler<SendEmailJob, SendEmailJobHandler>();
                services.AddJobHandler<ProcessDataJob, ProcessDataJobHandler>();
                services.AddJobHandler<GenerateReportJob, GenerateReportJobHandler>();
                services.AddJobHandler<CleanupJob, CleanupJobHandler>();
                services.AddJobHandler<SendNotificationJob, SendNotificationJobHandler>();
            })
            .ConfigureLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddConsole();
            });

    private static async Task RunDemonstrationAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var jobClient = scope.ServiceProvider.GetRequiredService<IJobClient>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Starting JobSharp demonstration...");

        try
        {
            // 1. Fire-and-Forget Jobs
            Console.WriteLine("\n🔥 1. Fire-and-Forget Jobs");
            await DemonstrateFireAndForgetJobs(jobClient, logger);

            // 2. Delayed Jobs
            Console.WriteLine("\n⏰ 2. Delayed Jobs");
            await DemonstrateDelayedJobs(jobClient, logger);

            // 3. Scheduled Jobs
            Console.WriteLine("\n📅 3. Scheduled Jobs");
            await DemonstrateScheduledJobs(jobClient, logger);

            // 4. Recurring Jobs
            Console.WriteLine("\n🔄 4. Recurring Jobs");
            await DemonstrateRecurringJobs(jobClient, logger);

            // 5. Continuation Jobs
            Console.WriteLine("\n➡️ 5. Continuation Jobs");
            await DemonstrateContinuationJobs(jobClient, logger);

            // 6. Batch Jobs
            Console.WriteLine("\n📦 6. Batch Jobs");
            await DemonstrateBatchJobs(jobClient, logger);

            // 7. Batch Continuations
            Console.WriteLine("\n📦➡️ 7. Batch Continuations");
            await DemonstrateBatchContinuations(jobClient, logger);

            // 8. Job Management
            Console.WriteLine("\n🔧 8. Job Management");
            await DemonstrateJobManagement(jobClient, logger);

            Console.WriteLine("\n✅ All demonstrations scheduled successfully!");
            Console.WriteLine("Watch the logs above to see jobs being processed...");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during demonstration");
        }
    }

    private static async Task DemonstrateFireAndForgetJobs(IJobClient jobClient, ILogger logger)
    {
        logger.LogInformation("Scheduling fire-and-forget jobs...");

        var emailJobId = await jobClient.EnqueueAsync(new SendEmailJob
        {
            To = "user@example.com",
            Subject = "Welcome to JobSharp!",
            Body = "This is a fire-and-forget email job.",
            From = "noreply@jobsharp.com"
        });

        var dataJobId = await jobClient.EnqueueAsync(new ProcessDataJob
        {
            DataId = "DATA-001",
            ProcessingType = "fast",
            Parameters = new Dictionary<string, object> { ["priority"] = "high" }
        });

        logger.LogInformation("Scheduled fire-and-forget jobs: {EmailJobId}, {DataJobId}", emailJobId, dataJobId);
    }

    private static async Task DemonstrateDelayedJobs(IJobClient jobClient, ILogger logger)
    {
        logger.LogInformation("Scheduling delayed jobs...");

        // Job delayed by 10 seconds
        var delayedEmailId = await jobClient.ScheduleAsync(new SendEmailJob
        {
            To = "delayed@example.com",
            Subject = "Delayed Email",
            Body = "This email was delayed by 10 seconds.",
            From = "scheduler@jobsharp.com"
        }, TimeSpan.FromSeconds(10));

        // Job delayed by 30 seconds
        var delayedReportId = await jobClient.ScheduleAsync(new GenerateReportJob
        {
            ReportType = "monthly",
            StartDate = DateTimeOffset.UtcNow.AddDays(-30),
            EndDate = DateTimeOffset.UtcNow,
            UserId = "user123"
        }, TimeSpan.FromSeconds(30));

        logger.LogInformation("Scheduled delayed jobs: {DelayedEmailId} (10s), {DelayedReportId} (30s)",
            delayedEmailId, delayedReportId);
    }

    private static async Task DemonstrateScheduledJobs(IJobClient jobClient, ILogger logger)
    {
        logger.LogInformation("Scheduling jobs for specific times...");

        // Job scheduled for 1 minute from now
        var futureTime = DateTimeOffset.UtcNow.AddMinutes(1);
        var scheduledCleanupId = await jobClient.ScheduleAsync(new CleanupJob
        {
            ResourceType = "temporary_files",
            MaxAge = TimeSpan.FromHours(24),
            BatchSize = 100
        }, futureTime);

        logger.LogInformation("Scheduled cleanup job {ScheduledCleanupId} for {FutureTime}",
            scheduledCleanupId, futureTime);
    }

    private static async Task DemonstrateRecurringJobs(IJobClient jobClient, ILogger logger)
    {
        logger.LogInformation("Setting up recurring jobs...");

        // Recurring job every 2 minutes
        await jobClient.AddOrUpdateRecurringJobAsync("daily-cleanup", new CleanupJob
        {
            ResourceType = "log_files",
            MaxAge = TimeSpan.FromDays(7),
            BatchSize = 50
        }, "*/2 * * * *"); // Every 2 minutes for demo purposes

        // Recurring job every 5 minutes
        await jobClient.AddOrUpdateRecurringJobAsync("system-health-check", new ProcessDataJob
        {
            DataId = "HEALTH-CHECK",
            ProcessingType = "fast",
            Parameters = new Dictionary<string, object> { ["check_type"] = "system" }
        }, "*/5 * * * *"); // Every 5 minutes

        logger.LogInformation("Set up recurring jobs: daily-cleanup (every 2 min), system-health-check (every 5 min)");
    }

    private static async Task DemonstrateContinuationJobs(IJobClient jobClient, ILogger logger)
    {
        logger.LogInformation("Scheduling continuation jobs...");

        // Parent job
        var reportJobId = await jobClient.EnqueueAsync(new GenerateReportJob
        {
            ReportType = "quarterly",
            StartDate = DateTimeOffset.UtcNow.AddDays(-90),
            EndDate = DateTimeOffset.UtcNow,
            UserId = "manager123"
        });

        // Continuation job that will run after the report is generated
        var notificationJobId = await jobClient.ContinueWithAsync(reportJobId, new SendNotificationJob
        {
            UserId = "manager123",
            Title = "Report Ready",
            Message = "Your quarterly report has been generated and is ready for download.",
            NotificationType = "Success"
        });

        logger.LogInformation("Scheduled continuation: report {ReportJobId} → notification {NotificationJobId}",
            reportJobId, notificationJobId);
    }

    private static async Task DemonstrateBatchJobs(IJobClient jobClient, ILogger logger)
    {
        logger.LogInformation("Scheduling batch jobs...");

        // Create a batch of email jobs
        var emailJobs = new[]
        {
            new SendEmailJob { To = "user1@example.com", Subject = "Batch Email 1", Body = "First email in batch" },
            new SendEmailJob { To = "user2@example.com", Subject = "Batch Email 2", Body = "Second email in batch" },
            new SendEmailJob { To = "user3@example.com", Subject = "Batch Email 3", Body = "Third email in batch" }
        };

        var (batchId, jobIds) = await jobClient.EnqueueBatchAsync(emailJobs);

        logger.LogInformation("Scheduled email batch {BatchId} with {JobCount} jobs: [{JobIds}]",
            batchId, jobIds.Count(), string.Join(", ", jobIds));

        // Create a batch of data processing jobs
        var dataJobs = Enumerable.Range(1, 5).Select(i => new ProcessDataJob
        {
            DataId = $"BATCH-DATA-{i:D3}",
            ProcessingType = i % 2 == 0 ? "fast" : "medium",
            Parameters = new Dictionary<string, object> { ["batch_id"] = batchId, ["sequence"] = i }
        });

        var (dataBatchId, dataJobIds) = await jobClient.EnqueueBatchAsync(dataJobs);

        logger.LogInformation("Scheduled data processing batch {DataBatchId} with {JobCount} jobs",
            dataBatchId, dataJobIds.Count());
    }

    private static async Task DemonstrateBatchContinuations(IJobClient jobClient, ILogger logger)
    {
        logger.LogInformation("Scheduling batch with continuation...");

        // Create a batch of report generation jobs
        var reportJobs = new[]
        {
            new GenerateReportJob { ReportType = "sales", StartDate = DateTimeOffset.UtcNow.AddDays(-7), EndDate = DateTimeOffset.UtcNow },
            new GenerateReportJob { ReportType = "inventory", StartDate = DateTimeOffset.UtcNow.AddDays(-7), EndDate = DateTimeOffset.UtcNow },
            new GenerateReportJob { ReportType = "financial", StartDate = DateTimeOffset.UtcNow.AddDays(-7), EndDate = DateTimeOffset.UtcNow }
        };

        var (batchId, jobIds) = await jobClient.EnqueueBatchAsync(reportJobs);

        // Schedule a continuation job that will run after all reports are completed
        var batchContinuationId = await jobClient.ContinueBatchWithAsync(batchId, new SendNotificationJob
        {
            UserId = "admin",
            Title = "Weekly Reports Complete",
            Message = "All weekly reports have been generated successfully.",
            NotificationType = "BatchComplete",
            Metadata = new Dictionary<string, string> { ["batch_id"] = batchId }
        });

        logger.LogInformation("Scheduled report batch {BatchId} with continuation {ContinuationId}",
            batchId, batchContinuationId);
    }

    private static async Task DemonstrateJobManagement(IJobClient jobClient, ILogger logger)
    {
        logger.LogInformation("Demonstrating job management features...");

        // Schedule a job and then cancel it
        var cancelableJobId = await jobClient.ScheduleAsync(new ProcessDataJob
        {
            DataId = "CANCELABLE-001",
            ProcessingType = "slow"
        }, TimeSpan.FromMinutes(2));

        logger.LogInformation("Scheduled cancelable job {CancelableJobId}", cancelableJobId);

        // Wait a moment then cancel it
        await Task.Delay(1000);
        var cancelled = await jobClient.CancelJobAsync(cancelableJobId);
        logger.LogInformation("Job {CancelableJobId} cancellation: {Cancelled}",
            cancelableJobId, cancelled ? "SUCCESS" : "FAILED");

        // Get job counts by state
        await Task.Delay(2000); // Wait for some jobs to be processed

        var scheduledCount = await jobClient.GetJobCountAsync(Core.JobState.Scheduled);
        var processingCount = await jobClient.GetJobCountAsync(Core.JobState.Processing);
        var succeededCount = await jobClient.GetJobCountAsync(Core.JobState.Succeeded);
        var failedCount = await jobClient.GetJobCountAsync(Core.JobState.Failed);

        logger.LogInformation("Job counts - Scheduled: {Scheduled}, Processing: {Processing}, Succeeded: {Succeeded}, Failed: {Failed}",
            scheduledCount, processingCount, succeededCount, failedCount);
    }
}
