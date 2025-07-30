# JobSharp Database Schema

JobSharp uses Entity Framework Core to manage persistent job storage. This document describes the database schema and table structures.

## Tables Overview

JobSharp creates two main tables:
- **Jobs** - Stores individual job instances
- **RecurringJobs** - Stores recurring job templates and schedules

## Jobs Table

The `Jobs` table stores all job instances including fire-and-forget, delayed, scheduled, continuation, and batch jobs.

### Schema

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | varchar(36) | NO | Primary key - unique job identifier (GUID) |
| `TypeName` | varchar(500) | NO | Full type name of the job class for deserialization |
| `Arguments` | text | YES | JSON-serialized job arguments |
| `State` | int | NO | Current job state (see JobState enum) |
| `CreatedAt` | datetimeoffset | NO | When the job was created |
| `ScheduledAt` | datetimeoffset | YES | When the job should be executed |
| `ExecutedAt` | datetimeoffset | YES | When the job was last executed |
| `RetryCount` | int | NO | Number of retry attempts made |
| `MaxRetryCount` | int | NO | Maximum allowed retry attempts |
| `ErrorMessage` | text | YES | Error message if job failed |
| `Result` | text | YES | JSON-serialized job result if successful |
| `BatchId` | varchar(36) | YES | Batch identifier if part of a batch |
| `ParentJobId` | varchar(36) | YES | Parent job ID for continuation jobs |

### JobState Enum Values

| Value | Name | Description |
|-------|------|-------------|
| 0 | Created | Job created but not scheduled |
| 1 | Scheduled | Job waiting to be executed |
| 2 | Processing | Job currently being executed |
| 3 | Succeeded | Job completed successfully |
| 4 | Failed | Job failed but may be retried |
| 5 | Cancelled | Job was cancelled |
| 6 | Abandoned | Job failed and exceeded retry limit |
| 7 | AwaitingContinuation | Job waiting for parent completion |
| 8 | AwaitingBatch | Job part of incomplete batch |

### Indexes

The following indexes are automatically created for optimal query performance:

- `IX_Jobs_State` - For filtering by job state
- `IX_Jobs_ScheduledAt` - For finding jobs ready to execute
- `IX_Jobs_BatchId` - For batch operations
- `IX_Jobs_ParentJobId` - For continuation jobs
- `IX_Jobs_State_ScheduledAt` - Composite index for scheduled job queries

### Foreign Keys

- `FK_Jobs_Jobs_ParentJobId` - Self-referencing foreign key for continuation jobs

## RecurringJobs Table

The `RecurringJobs` table stores recurring job templates and their scheduling information.

### Schema

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | varchar(200) | NO | Primary key - unique recurring job identifier |
| `CronExpression` | varchar(100) | NO | Cron expression defining the schedule |
| `JobTypeName` | varchar(500) | NO | Type name of the job to create |
| `JobArguments` | text | YES | JSON-serialized job arguments template |
| `MaxRetryCount` | int | NO | Max retry count for created job instances |
| `NextExecution` | datetimeoffset | YES | Next scheduled execution time |
| `LastExecution` | datetimeoffset | YES | Last execution time |
| `IsEnabled` | bit | NO | Whether the recurring job is active |
| `CreatedAt` | datetimeoffset | NO | When the recurring job was created |

### Indexes

- `IX_RecurringJobs_IsEnabled` - For filtering active recurring jobs
- `IX_RecurringJobs_NextExecution` - For finding jobs ready to schedule

## Sample SQL Schema (SQL Server)

```sql
-- Jobs table
CREATE TABLE [Jobs] (
    [Id] nvarchar(36) NOT NULL,
    [TypeName] nvarchar(500) NOT NULL,
    [Arguments] nvarchar(max) NULL,
    [State] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ScheduledAt] datetimeoffset NULL,
    [ExecutedAt] datetimeoffset NULL,
    [RetryCount] int NOT NULL,
    [MaxRetryCount] int NOT NULL,
    [ErrorMessage] nvarchar(max) NULL,
    [Result] nvarchar(max) NULL,
    [BatchId] nvarchar(36) NULL,
    [ParentJobId] nvarchar(36) NULL,
    CONSTRAINT [PK_Jobs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Jobs_Jobs_ParentJobId] FOREIGN KEY ([ParentJobId]) REFERENCES [Jobs] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Jobs_BatchId] ON [Jobs] ([BatchId]);
CREATE INDEX [IX_Jobs_ParentJobId] ON [Jobs] ([ParentJobId]);
CREATE INDEX [IX_Jobs_ScheduledAt] ON [Jobs] ([ScheduledAt]);
CREATE INDEX [IX_Jobs_State] ON [Jobs] ([State]);
CREATE INDEX [IX_Jobs_State_ScheduledAt] ON [Jobs] ([State], [ScheduledAt]);

-- RecurringJobs table
CREATE TABLE [RecurringJobs] (
    [Id] nvarchar(200) NOT NULL,
    [CronExpression] nvarchar(100) NOT NULL,
    [JobTypeName] nvarchar(500) NOT NULL,
    [JobArguments] nvarchar(max) NULL,
    [MaxRetryCount] int NOT NULL,
    [NextExecution] datetimeoffset NULL,
    [LastExecution] datetimeoffset NULL,
    [IsEnabled] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_RecurringJobs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_RecurringJobs_IsEnabled] ON [RecurringJobs] ([IsEnabled]);
CREATE INDEX [IX_RecurringJobs_NextExecution] ON [RecurringJobs] ([NextExecution]);
```

## Sample SQL Schema (SQLite)

```sql
-- Jobs table
CREATE TABLE "Jobs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Jobs" PRIMARY KEY,
    "TypeName" TEXT NOT NULL,
    "Arguments" TEXT,
    "State" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "ScheduledAt" TEXT,
    "ExecutedAt" TEXT,
    "RetryCount" INTEGER NOT NULL,
    "MaxRetryCount" INTEGER NOT NULL,
    "ErrorMessage" TEXT,
    "Result" TEXT,
    "BatchId" TEXT,
    "ParentJobId" TEXT,
    CONSTRAINT "FK_Jobs_Jobs_ParentJobId" FOREIGN KEY ("ParentJobId") REFERENCES "Jobs" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Jobs_BatchId" ON "Jobs" ("BatchId");
CREATE INDEX "IX_Jobs_ParentJobId" ON "Jobs" ("ParentJobId");
CREATE INDEX "IX_Jobs_ScheduledAt" ON "Jobs" ("ScheduledAt");
CREATE INDEX "IX_Jobs_State" ON "Jobs" ("State");
CREATE INDEX "IX_Jobs_State_ScheduledAt" ON "Jobs" ("State", "ScheduledAt");

-- RecurringJobs table
CREATE TABLE "RecurringJobs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_RecurringJobs" PRIMARY KEY,
    "CronExpression" TEXT NOT NULL,
    "JobTypeName" TEXT NOT NULL,
    "JobArguments" TEXT,
    "MaxRetryCount" INTEGER NOT NULL,
    "NextExecution" TEXT,
    "LastExecution" TEXT,
    "IsEnabled" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL
);

CREATE INDEX "IX_RecurringJobs_IsEnabled" ON "RecurringJobs" ("IsEnabled");
CREATE INDEX "IX_RecurringJobs_NextExecution" ON "RecurringJobs" ("NextExecution");
```

## Common Queries

### Find Scheduled Jobs Ready for Execution

```sql
SELECT * FROM Jobs 
WHERE State = 1 -- Scheduled
  AND ScheduledAt <= GETUTCDATE() -- SQL Server
  -- AND ScheduledAt <= datetime('now') -- SQLite
ORDER BY ScheduledAt
```

### Find Jobs in a Batch

```sql
SELECT * FROM Jobs 
WHERE BatchId = @BatchId
ORDER BY CreatedAt
```

### Find Continuation Jobs for a Parent

```sql
SELECT * FROM Jobs 
WHERE ParentJobId = @ParentJobId 
  AND State = 7 -- AwaitingContinuation
```

### Find Active Recurring Jobs

```sql
SELECT * FROM RecurringJobs 
WHERE IsEnabled = 1
  AND (NextExecution IS NULL OR NextExecution <= GETUTCDATE())
```

### Job Statistics

```sql
-- Count jobs by state
SELECT State, COUNT(*) as Count
FROM Jobs 
GROUP BY State

-- Failed jobs in last 24 hours
SELECT COUNT(*) as FailedJobs
FROM Jobs 
WHERE State IN (4, 6) -- Failed or Abandoned
  AND CreatedAt >= DATEADD(hour, -24, GETUTCDATE()) -- SQL Server
  -- AND CreatedAt >= datetime('now', '-24 hours') -- SQLite
```

## Migration and Setup

### Using EF Core Migrations

```bash
# Add migration
dotnet ef migrations add InitialCreate --project JobSharp.EntityFramework

# Update database
dotnet ef database update --project JobSharp.EntityFramework
```

### Programmatic Setup

```csharp
// Ensure database is created
await serviceProvider.EnsureJobSharpDatabaseCreatedAsync();

// Or run migrations
await serviceProvider.MigrateJobSharpDatabaseAsync();
```

## Maintenance Queries

### Cleanup Completed Jobs

```sql
-- Delete successful jobs older than 30 days
DELETE FROM Jobs 
WHERE State = 3 -- Succeeded 
  AND CreatedAt < DATEADD(day, -30, GETUTCDATE()) -- SQL Server
  -- AND CreatedAt < datetime('now', '-30 days') -- SQLite

-- Delete abandoned jobs older than 7 days  
DELETE FROM Jobs 
WHERE State = 6 -- Abandoned
  AND CreatedAt < DATEADD(day, -7, GETUTCDATE()) -- SQL Server
  -- AND CreatedAt < datetime('now', '-7 days') -- SQLite
```

### Reset Failed Jobs for Retry

```sql
UPDATE Jobs 
SET State = 1, -- Scheduled
    RetryCount = 0,
    ScheduledAt = GETUTCDATE(), -- SQL Server
    -- ScheduledAt = datetime('now'), -- SQLite
    ErrorMessage = NULL
WHERE State = 4 -- Failed
  AND RetryCount < MaxRetryCount
```

## Performance Considerations

1. **Indexing**: The provided indexes should handle most query patterns efficiently
2. **Partitioning**: For high-volume scenarios, consider partitioning the Jobs table by CreatedAt
3. **Archival**: Implement regular cleanup of old completed jobs
4. **Connection Pooling**: Use connection pooling for better performance
5. **Bulk Operations**: Use EF Core bulk operations for large batch inserts

## Monitoring Queries

```sql
-- Jobs pending execution
SELECT COUNT(*) FROM Jobs WHERE State = 1 AND ScheduledAt <= GETUTCDATE()

-- Jobs currently processing  
SELECT COUNT(*) FROM Jobs WHERE State = 2

-- Average job execution time (for completed jobs)
SELECT AVG(DATEDIFF(second, ScheduledAt, ExecutedAt)) as AvgExecutionSeconds
FROM Jobs 
WHERE State = 3 AND ExecutedAt IS NOT NULL AND ScheduledAt IS NOT NULL

-- Most common job types
SELECT TypeName, COUNT(*) as Count
FROM Jobs 
GROUP BY TypeName 
ORDER BY Count DESC
``` 