-- JobSharp Dapper MySQL Schema
-- Jobs table
CREATE TABLE Jobs (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    TypeName VARCHAR(500) NOT NULL,
    Arguments LONGTEXT NULL,
    State INT NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    ScheduledAt DATETIME(6) NULL,
    ExecutedAt DATETIME(6) NULL,
    RetryCount INT NOT NULL DEFAULT 0,
    MaxRetryCount INT NOT NULL DEFAULT 0,
    ErrorMessage LONGTEXT NULL,
    Result LONGTEXT NULL,
    BatchId VARCHAR(36) NULL,
    ParentJobId VARCHAR(36) NULL
);
-- RecurringJobs table
CREATE TABLE RecurringJobs (
    Id VARCHAR(200) NOT NULL PRIMARY KEY,
    CronExpression VARCHAR(100) NOT NULL,
    JobTypeName VARCHAR(500) NOT NULL,
    JobArguments LONGTEXT NULL,
    MaxRetryCount INT NOT NULL DEFAULT 0,
    NextExecution DATETIME(6) NULL,
    LastExecution DATETIME(6) NULL,
    IsEnabled BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt DATETIME(6) NOT NULL
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