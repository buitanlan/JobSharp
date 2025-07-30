-- JobSharp Dapper SQL Server Schema
-- Jobs table
CREATE TABLE Jobs (
    Id NVARCHAR(36) NOT NULL PRIMARY KEY,
    TypeName NVARCHAR(500) NOT NULL,
    Arguments NVARCHAR(MAX) NULL,
    State INT NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    ScheduledAt DATETIMEOFFSET NULL,
    ExecutedAt DATETIMEOFFSET NULL,
    RetryCount INT NOT NULL DEFAULT 0,
    MaxRetryCount INT NOT NULL DEFAULT 0,
    ErrorMessage NVARCHAR(MAX) NULL,
    Result NVARCHAR(MAX) NULL,
    BatchId NVARCHAR(36) NULL,
    ParentJobId NVARCHAR(36) NULL
);
-- RecurringJobs table
CREATE TABLE RecurringJobs (
    Id NVARCHAR(200) NOT NULL PRIMARY KEY,
    CronExpression NVARCHAR(100) NOT NULL,
    JobTypeName NVARCHAR(500) NOT NULL,
    JobArguments NVARCHAR(MAX) NULL,
    MaxRetryCount INT NOT NULL DEFAULT 0,
    NextExecution DATETIMEOFFSET NULL,
    LastExecution DATETIMEOFFSET NULL,
    IsEnabled BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIMEOFFSET NOT NULL
);
-- Indexes for better query performance
CREATE INDEX IX_Jobs_State ON Jobs (State);
CREATE INDEX IX_Jobs_ScheduledAt ON Jobs (ScheduledAt);
CREATE INDEX IX_Jobs_BatchId ON Jobs (BatchId);
CREATE INDEX IX_Jobs_ParentJobId ON Jobs (ParentJobId);
CREATE INDEX IX_Jobs_State_ScheduledAt ON Jobs (State, ScheduledAt);
CREATE INDEX IX_RecurringJobs_IsEnabled ON RecurringJobs (IsEnabled);
CREATE INDEX IX_RecurringJobs_NextExecution ON RecurringJobs (NextExecution);
-- Foreign key constraints
ALTER TABLE Jobs
ADD CONSTRAINT FK_Jobs_ParentJob FOREIGN KEY (ParentJobId) REFERENCES Jobs(Id) ON DELETE CASCADE;