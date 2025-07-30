# JobSharp - A Comprehensive Job Processing Library for .NET

JobSharp is a powerful, feature-rich job processing library for .NET 8.0+ that provides support for Fire-and-Forget Jobs, Delayed Jobs, Recurring Jobs, Continuations, Batches, Batch Continuations, and persistent job storage with RDBMS support through Entity Framework Core.

## 🚀 Features

- **Fire-and-Forget Jobs**: Execute jobs immediately in the background
- **Delayed Jobs**: Schedule jobs to run after a specified delay or at a specific time
- **Recurring Jobs**: Schedule jobs to run on a recurring basis using cron expressions (custom parser, no external dependencies)
- **Continuation Jobs**: Chain jobs to run after parent jobs complete successfully
- **Batch Jobs**: Execute multiple jobs as a batch
- **Batch Continuations**: Execute jobs after all jobs in a batch complete
- **Persistent Storage**: Store jobs in SQL Server, SQLite, or any EF Core supported database
- **Retry Logic**: Automatic retry with configurable policies
- **Dependency Injection**: Full support for .NET dependency injection
- **Background Processing**: Built-in background service for processing jobs
- **Job Management**: Cancel, delete, and monitor job states
- **Zero External Dependencies**: Only uses .NET standard libraries and Entity Framework Core

## 📦 Packages

- **JobSharp**: Core library with job scheduling and processing
- **JobSharp.EntityFramework**: Entity Framework Core integration for persistent storage

## 🛠️ Installation

```bash
# Core library
dotnet add package JobSharp

# Entity Framework integration
dotnet add package JobSharp.EntityFramework
```

## 🏁 Quick Start

### 1. Configure Services

```csharp
using JobSharp.Extensions;
using JobSharp.EntityFramework.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Configure JobSharp with SQLite storage
builder.Services.AddJobSharpEntityFrameworkSqlite("Data Source=jobs.db");

// Configure JobSharp processor
builder.Services.AddJobSharp(options =>
{
    options.MaxConcurrentJobs = 10;
    options.PollingInterval = TimeSpan.FromSeconds(5);
});

// Register job handlers
builder.Services.AddJobHandler<SendEmailJob, SendEmailJobHandler>();

var host = builder.Build();

// Ensure database is created
await host.Services.EnsureJobSharpDatabaseCreatedAsync();

await host.RunAsync();
```

### 2. Define Job Types

```csharp
public class SendEmailJob
{
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
}
```

### 3. Create Job Handlers

```csharp
using JobSharp.Core;

public class SendEmailJobHandler : JobHandlerBase<SendEmailJob>
{
    private readonly ILogger<SendEmailJobHandler> _logger;

    public SendEmailJobHandler(ILogger<SendEmailJobHandler> logger)
    {
        _logger = logger;
    }

    public override async Task<JobExecutionResult> HandleAsync(SendEmailJob job, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending email to {To}", job.To);
        
        try
        {
            // Your email sending logic here
            await SendEmailAsync(job);
            
            return JobExecutionResult.Success();
        }
        catch (Exception ex)
        {
            return JobExecutionResult.Failure(ex, shouldRetry: true);
        }
    }
}
```

### 4. Schedule Jobs

```csharp
public class EmailService
{
    private readonly IJobClient _jobClient;

    public EmailService(IJobClient jobClient)
    {
        _jobClient = jobClient;
    }

    // Fire-and-forget job
    public async Task<string> SendWelcomeEmailAsync(string email)
    {
        return await _jobClient.EnqueueAsync(new SendEmailJob
        {
            To = email,
            Subject = "Welcome!",
            Body = "Welcome to our service!"
        });
    }

    // Delayed job
    public async Task<string> SendReminderEmailAsync(string email, TimeSpan delay)
    {
        return await _jobClient.ScheduleAsync(new SendEmailJob
        {
            To = email,
            Subject = "Reminder",
            Body = "Don't forget to complete your profile!"
        }, delay);
    }

    // Recurring job
    public async Task SetupDailyNewsletterAsync()
    {
        await _jobClient.AddOrUpdateRecurringJobAsync("daily-newsletter", 
            new SendEmailJob
            {
                To = "subscribers@example.com",
                Subject = "Daily Newsletter",
                Body = "Today's news..."
            }, 
            "0 9 * * *"); // Every day at 9 AM
    }
}
```

## 📋 Job Types

### Fire-and-Forget Jobs

Execute jobs immediately in the background:

```csharp
var jobId = await jobClient.EnqueueAsync(new ProcessDataJob
{
    DataId = "12345",
    ProcessingType = "standard"
});
```

### Delayed Jobs

Schedule jobs to run after a delay or at a specific time:

```csharp
// Delay by timespan
var jobId = await jobClient.ScheduleAsync(new SendEmailJob(...), TimeSpan.FromHours(2));

// Schedule for specific time
var jobId = await jobClient.ScheduleAsync(new GenerateReportJob(...), DateTimeOffset.Parse("2024-01-01 09:00"));
```

### Recurring Jobs

Schedule jobs using cron expressions:

```csharp
// Every day at midnight
await jobClient.AddOrUpdateRecurringJobAsync("daily-cleanup", 
    new CleanupJob { ResourceType = "temp_files" }, 
    "0 0 * * *");

// Every 15 minutes
await jobClient.AddOrUpdateRecurringJobAsync("health-check", 
    new HealthCheckJob(), 
    "*/15 * * * *");
```

### Continuation Jobs

Chain jobs to execute after parent jobs complete:

```csharp
var parentJobId = await jobClient.EnqueueAsync(new GenerateReportJob(...));

var continuationJobId = await jobClient.ContinueWithAsync(parentJobId, 
    new SendEmailJob
    {
        To = "manager@company.com",
        Subject = "Report Ready",
        Body = "Your report has been generated."
    });
```

### Batch Jobs

Execute multiple jobs as a batch:

```csharp
var emailJobs = new[]
{
    new SendEmailJob { To = "user1@example.com", Subject = "Notification", Body = "Message 1" },
    new SendEmailJob { To = "user2@example.com", Subject = "Notification", Body = "Message 2" },
    new SendEmailJob { To = "user3@example.com", Subject = "Notification", Body = "Message 3" }
};

var (batchId, jobIds) = await jobClient.EnqueueBatchAsync(emailJobs);
```

### Batch Continuations

Execute a job after all jobs in a batch complete:

```csharp
var (batchId, jobIds) = await jobClient.EnqueueBatchAsync(reportJobs);

var batchContinuationId = await jobClient.ContinueBatchWithAsync(batchId,
    new SendNotificationJob
    {
        UserId = "admin",
        Title = "Batch Complete",
        Message = "All reports have been generated."
    });
```

## 🔧 Configuration

Configure JobSharp processor options:

```csharp
services.AddJobSharp(options =>
{
    options.MaxConcurrentJobs = 10;              // Max jobs running concurrently
    options.PollingInterval = TimeSpan.FromSeconds(5);  // How often to check for new jobs
    options.RecurringJobsPollingInterval = TimeSpan.FromMinutes(1);  // Recurring jobs check interval
    options.DefaultRetryDelay = TimeSpan.FromSeconds(30);  // Default delay between retries
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);    // Max time to wait for jobs on shutdown
});
```

## 🗄️ Database Support

JobSharp supports multiple databases through Entity Framework Core:

### SQL Server

```csharp
services.AddJobSharpEntityFramework("Server=localhost;Database=JobSharp;Trusted_Connection=true;");
```

### SQLite

```csharp
services.AddJobSharpEntityFrameworkSqlite("Data Source=jobs.db");
```

### Custom Configuration

```csharp
services.AddJobSharpEntityFramework(options =>
{
    options.UseNpgsql("Host=localhost;Database=jobsharp;Username=user;Password=pass");
    options.EnableSensitiveDataLogging();
});
```

## 🔄 Job States

Jobs can be in one of the following states:

- **Created**: Job has been created but not scheduled
- **Scheduled**: Job is waiting to be executed
- **Processing**: Job is currently being executed
- **Succeeded**: Job completed successfully
- **Failed**: Job failed but may be retried
- **Cancelled**: Job was cancelled before execution
- **Abandoned**: Job failed and exceeded retry limits
- **AwaitingContinuation**: Job is waiting for parent to complete
- **AwaitingBatch**: Job is part of an incomplete batch

## 📊 Job Management

### Monitor Jobs

```csharp
// Get job information
var job = await jobClient.GetJobAsync(jobId);

// Get job counts by state
var scheduledCount = await jobClient.GetJobCountAsync(JobState.Scheduled);
var succeededCount = await jobClient.GetJobCountAsync(JobState.Succeeded);
```

### Cancel Jobs

```csharp
var cancelled = await jobClient.CancelJobAsync(jobId);
```

### Delete Jobs

```csharp
await jobClient.DeleteJobAsync(jobId);
```

## 🧪 Testing

The library includes a comprehensive example application demonstrating all features:

```bash
cd JobSharp.Example
dotnet run
```

## 🏗️ Architecture

- **JobSharp**: Core library with interfaces, job processing, and scheduling
- **JobSharp.EntityFramework**: Entity Framework Core integration
- **JobSharp.Example**: Example application demonstrating all features

## 📊 Database Schema

For detailed information about the database tables, indexes, and schema, see [Database Schema Documentation](docs/DATABASE_SCHEMA.md).

Key tables:
- **Jobs**: Stores all job instances with state, scheduling, and execution information
- **RecurringJobs**: Stores recurring job templates and cron schedules

## 📄 License

This project is licensed under the MIT License.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📞 Support

For questions and support, please open an issue on the GitHub repository. 