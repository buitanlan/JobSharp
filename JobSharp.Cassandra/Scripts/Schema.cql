-- JobSharp Cassandra Schema

-- Create keyspace (adjust replication settings as needed)
-- CREATE KEYSPACE IF NOT EXISTS jobsharp
-- WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 1};

-- USE jobsharp;

-- Main jobs table
CREATE TABLE IF NOT EXISTS jobs (
    id text PRIMARY KEY,
    type_name text,
    arguments text,
    state int,
    created_at timestamp,
    scheduled_at timestamp,
    executed_at timestamp,
    retry_count int,
    max_retry_count int,
    error_message text,
    result text,
    batch_id text,
    parent_job_id text
);

-- Jobs by state table for efficient state-based queries
CREATE TABLE IF NOT EXISTS jobs_by_state (
    state int,
    created_at timestamp,
    job_id text,
    PRIMARY KEY (state, created_at, job_id)
) WITH CLUSTERING ORDER BY (created_at ASC, job_id ASC);

-- Scheduled jobs table for efficient scheduled job queries
-- Using bucketing strategy to distribute load
CREATE TABLE IF NOT EXISTS scheduled_jobs (
    bucket int,
    scheduled_at timestamp,
    job_id text,
    PRIMARY KEY (bucket, scheduled_at, job_id)
) WITH CLUSTERING ORDER BY (scheduled_at ASC, job_id ASC);

-- Recurring jobs table
CREATE TABLE IF NOT EXISTS recurring_jobs (
    id text PRIMARY KEY,
    cron_expression text,
    job_type_name text,
    job_arguments text,
    max_retry_count int,
    next_execution timestamp,
    last_execution timestamp,
    is_enabled boolean,
    created_at timestamp
);

-- Create secondary indexes for filtering
CREATE INDEX IF NOT EXISTS jobs_batch_id_idx ON jobs (batch_id);
CREATE INDEX IF NOT EXISTS jobs_parent_job_id_idx ON jobs (parent_job_id);
CREATE INDEX IF NOT EXISTS recurring_jobs_enabled_idx ON recurring_jobs (is_enabled); 