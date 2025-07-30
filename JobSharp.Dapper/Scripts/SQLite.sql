-- JobSharp Dapper SQLite Schema
-- Jobs table
CREATE TABLE Jobs (
    Id TEXT NOT NULL PRIMARY KEY,
    TypeName TEXT NOT NULL,
    Arguments TEXT NULL,
    State INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    ScheduledAt TEXT NULL,
    ExecutedAt TEXT NULL,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    MaxRetryCount INTEGER NOT NULL DEFAULT 0,
    ErrorMessage TEXT NULL,
    Result TEXT NULL,
    BatchId TEXT NULL,
    ParentJobId TEXT NULL,
    FOREIGN KEY (ParentJobId) REFERENCES Jobs(Id) ON DELETE CASCADE
);
-- RecurringJobs table
CREATE TABLE RecurringJobs (
    Id TEXT NOT NULL PRIMARY KEY,
    CronExpression TEXT NOT NULL,
    JobTypeName TEXT NOT NULL,
    JobArguments TEXT NULL,
    MaxRetryCount INTEGER NOT NULL DEFAULT 0,
    NextExecution TEXT NULL,
    LastExecution TEXT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL
);
-- Indexes for better query performance
CREATE INDEX IX_Jobs_State ON Jobs (State);
CREATE INDEX IX_Jobs_ScheduledAt ON Jobs (ScheduledAt);
CREATE INDEX IX_Jobs_BatchId ON Jobs (BatchId);
CREATE INDEX IX_Jobs_ParentJobId ON Jobs (ParentJobId);
CREATE INDEX IX_Jobs_State_ScheduledAt ON Jobs (State, ScheduledAt);
CREATE INDEX IX_RecurringJobs_IsEnabled ON RecurringJobs (IsEnabled);
CREATE INDEX IX_RecurringJobs_NextExecution ON RecurringJobs (NextExecution);