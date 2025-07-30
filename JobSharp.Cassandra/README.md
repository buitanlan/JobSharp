# JobSharp.Cassandra

A Cassandra-based storage provider for JobSharp that leverages Apache Cassandra's distributed, wide-column database for highly scalable job storage and processing.

## Features

- **Massive Scalability**: Handles millions of jobs across multiple nodes
- **High Availability**: No single point of failure with Cassandra's distributed architecture
- **Optimized Data Model**: Uses multiple tables optimized for different query patterns
- **Automatic Partitioning**: Smart bucketing strategy for scheduled jobs
- **Tunable Consistency**: Configure consistency levels based on your requirements

## Installation

```bash
dotnet add package JobSharp.Cassandra
```

## Quick Start

### Basic Configuration

```csharp
using JobSharp.Cassandra.Extensions;

var contactPoints = new[] { "127.0.0.1" };
services.AddJobSharpCassandra(contactPoints, "jobsharp");
```

### With Custom Port

```csharp
using JobSharp.Cassandra.Extensions;

var contactPoints = new[] { "node1.cassandra.local", "node2.cassandra.local" };
services.AddJobSharpCassandra(contactPoints, "jobsharp", port: 9042);
```

### With Custom Cluster Configuration

```csharp
using JobSharp.Cassandra.Extensions;
using Cassandra;

services.AddJobSharpCassandra(builder =>
{
    return builder
        .AddContactPoints("node1", "node2", "node3")
        .WithPort(9042)
        .WithCredentials("username", "password")
        .WithReconnectionPolicy(new ExponentialReconnectionPolicy(1000, 10 * 60 * 1000))
        .WithRetryPolicy(new DefaultRetryPolicy());
}, "jobsharp");
```

### Custom Session Factory

```csharp
using JobSharp.Cassandra.Extensions;

services.AddJobSharpCassandra(serviceProvider =>
{
    var cluster = Cluster.Builder()
        .AddContactPoint("127.0.0.1")
        .Build();
    return cluster.Connect("jobsharp");
});
```

## Database Setup

Before using JobSharp.Cassandra, you need to create the keyspace and tables using the provided CQL script.

### Create Keyspace and Tables

```cql
-- Create keyspace (adjust replication settings for your environment)
CREATE KEYSPACE IF NOT EXISTS jobsharp
WITH replication = {
    'class': 'NetworkTopologyStrategy',
    'datacenter1': 3
};

USE jobsharp;

-- Run the schema script
SOURCE 'Scripts/Schema.cql';
```

### Production Replication Settings

```cql
-- For production with multiple datacenters
CREATE KEYSPACE IF NOT EXISTS jobsharp
WITH replication = {
    'class': 'NetworkTopologyStrategy',
    'dc1': 3,
    'dc2': 3
};

-- For single datacenter production
CREATE KEYSPACE IF NOT EXISTS jobsharp
WITH replication = {
    'class': 'NetworkTopologyStrategy',
    'datacenter1': 3
};
```

## Data Model

JobSharp.Cassandra uses a denormalized data model optimized for Cassandra:

### Tables

1. **jobs**: Main table storing complete job data (partitioned by job ID)
2. **jobs_by_state**: Optimized for state-based queries (partitioned by state)
3. **scheduled_jobs**: Optimized for scheduled job queries (partitioned by bucket)
4. **recurring_jobs**: Stores recurring job configurations (partitioned by job ID)

### Query Patterns

- **Get job by ID**: Direct lookup in `jobs` table
- **Get jobs by state**: Query `jobs_by_state` table, then fetch from `jobs`
- **Get scheduled jobs**: Query multiple buckets in `scheduled_jobs` table
- **Batch operations**: Multiple parallel operations across partitions

## Complete Example

```csharp
using JobSharp.Extensions;
using JobSharp.Cassandra.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add JobSharp services
builder.Services.AddJobSharp();

// Add Cassandra storage
var contactPoints = new[] { "cassandra-node1", "cassandra-node2", "cassandra-node3" };
builder.Services.AddJobSharpCassandra(contactPoints, "jobsharp");

var app = builder.Build();

// Use JobSharp
app.UseJobSharp();

app.Run();
```

## Configuration Examples

### Single Node Development

```csharp
var contactPoints = new[] { "127.0.0.1" };
services.AddJobSharpCassandra(contactPoints, "jobsharp");
```

### Multi-Node Cluster

```csharp
var contactPoints = new[] {
    "cassandra-node1.internal",
    "cassandra-node2.internal",
    "cassandra-node3.internal"
};
services.AddJobSharpCassandra(contactPoints, "jobsharp");
```

### With Authentication and SSL

```csharp
services.AddJobSharpCassandra(builder =>
{
    return builder
        .AddContactPoints("secure-cassandra.cloud.com")
        .WithPort(9142)
        .WithCredentials("username", "password")
        .WithSSL(new SSLOptions()
            .SetHostNameResolver((ipAddress) => "secure-cassandra.cloud.com"));
}, "jobsharp");
```

## Performance Optimization

### Consistency Levels

```csharp
// Configure for strong consistency
services.AddJobSharpCassandra(builder =>
{
    return builder
        .AddContactPoints("node1", "node2", "node3")
        .WithQueryOptions(new QueryOptions()
            .SetConsistencyLevel(ConsistencyLevel.Quorum));
}, "jobsharp");
```

### Connection Pooling

```csharp
services.AddJobSharpCassandra(builder =>
{
    return builder
        .AddContactPoints("node1", "node2", "node3")
        .WithPoolingOptions(new PoolingOptions()
            .SetCoreConnectionsPerHost(HostDistance.Local, 2)
            .SetMaxConnectionsPerHost(HostDistance.Local, 8));
}, "jobsharp");
```

## Table Structure

### Jobs Table

```cql
CREATE TABLE jobs (
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
```

### Jobs By State Table

```cql
CREATE TABLE jobs_by_state (
    state int,
    created_at timestamp,
    job_id text,
    PRIMARY KEY (state, created_at, job_id)
) WITH CLUSTERING ORDER BY (created_at ASC, job_id ASC);
```

### Scheduled Jobs Table

```cql
CREATE TABLE scheduled_jobs (
    bucket int,
    scheduled_at timestamp,
    job_id text,
    PRIMARY KEY (bucket, scheduled_at, job_id)
) WITH CLUSTERING ORDER BY (scheduled_at ASC, job_id ASC);
```

## Monitoring and Maintenance

### Cassandra Monitoring

```bash
# Check cluster status
nodetool status

# Monitor compaction
nodetool compactionstats

# Check table statistics
nodetool tablestats jobsharp
```

### Query Performance

```cql
-- Check for expensive queries (avoid ALLOW FILTERING when possible)
SELECT * FROM jobs WHERE batch_id = 'some-batch-id' ALLOW FILTERING;

-- Prefer partition key queries
SELECT * FROM jobs_by_state WHERE state = 1;
```

### Cleanup Operations

```cql
-- TTL can be set for automatic cleanup
ALTER TABLE jobs_by_state WITH default_time_to_live = 2592000; -- 30 days

-- Manual cleanup of old jobs
DELETE FROM jobs WHERE id IN ('old-job-id-1', 'old-job-id-2');
```

## Scaling Considerations

### Adding Nodes

```bash
# Add new nodes to the cluster
# Cassandra will automatically rebalance data
nodetool status
```

### Repair Operations

```bash
# Regular repair for data consistency
nodetool repair jobsharp

# Incremental repair for production
nodetool repair -inc jobsharp
```

## Troubleshooting

### Connection Issues
- Verify Cassandra cluster is healthy with `nodetool status`
- Check contact points are reachable
- Verify keyspace exists and has proper replication
- Check authentication credentials

### Performance Issues
- Monitor GC and compaction with `nodetool tpstats`
- Check for expensive queries with `ALLOW FILTERING`
- Verify proper partition key usage
- Monitor read/write latencies

### Consistency Issues
- Understand your consistency level requirements
- Use appropriate consistency levels for reads/writes
- Consider using `nodetool repair` for data consistency
- Monitor hinted handoff for temporary node failures

### Common Errors

- **"Keyspace not found"**: Create the keyspace first
- **"Table not found"**: Run the schema creation script
- **"No alive connections"**: Check contact points and network connectivity
- **"Query timeout"**: Increase timeout settings or optimize queries 