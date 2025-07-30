# JobSharp.Redis

A Redis-based storage provider for JobSharp that leverages Redis's high-performance key-value store and advanced data structures for job storage and processing.

## Features

- **High Performance**: Redis's in-memory storage provides exceptional speed
- **Advanced Data Structures**: Uses Redis sets, sorted sets, and strings optimally
- **Scalable**: Supports Redis clustering and replication
- **Flexible Configuration**: Multiple configuration options for different Redis setups
- **JSON Serialization**: Efficient job data serialization using System.Text.Json

## Installation

```bash
dotnet add package JobSharp.Redis
```

## Quick Start

### Basic Configuration

```csharp
using JobSharp.Redis.Extensions;

services.AddJobSharpRedis("localhost:6379");
```

### With Database Selection

```csharp
using JobSharp.Redis.Extensions;

services.AddJobSharpRedis("localhost:6379", database: 1);
```

### With Custom Configuration Options

```csharp
using JobSharp.Redis.Extensions;
using StackExchange.Redis;

var configOptions = new ConfigurationOptions
{
    EndPoints = { "localhost:6379" },
    Password = "your-password",
    ConnectTimeout = 5000,
    SyncTimeout = 5000
};

services.AddJobSharpRedis(configOptions, database: 0);
```

### Custom Database Factory

```csharp
using JobSharp.Redis.Extensions;

services.AddJobSharpRedis(serviceProvider =>
{
    var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
        .GetConnectionString("Redis");
    var multiplexer = ConnectionMultiplexer.Connect(connectionString);
    return multiplexer.GetDatabase(0);
});
```

## Redis Data Structure

JobSharp.Redis uses a well-designed key structure for optimal performance:

### Key Patterns

- `jobsharp:jobs:{jobId}` - Individual job data (JSON)
- `jobsharp:jobs:state:{stateNumber}` - Sets of job IDs by state
- `jobsharp:jobs:scheduled` - Sorted set of scheduled jobs (by timestamp)
- `jobsharp:jobs:batch:{batchId}` - Set of job IDs in a batch
- `jobsharp:jobs:continuation:{parentJobId}` - Set of continuation job IDs
- `jobsharp:recurring:{jobId}` - Individual recurring job data (JSON)
- `jobsharp:recurring:all` - Set of all recurring job IDs

### Data Structures Used

1. **Strings**: Store JSON-serialized job and recurring job data
2. **Sets**: Group jobs by state, batch, or continuation relationships
3. **Sorted Sets**: Order scheduled jobs by execution time for efficient querying

## Complete Example

```csharp
using JobSharp.Extensions;
using JobSharp.Redis.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add JobSharp services
builder.Services.AddJobSharp();

// Add Redis storage
builder.Services.AddJobSharpRedis("localhost:6379", database: 1);

var app = builder.Build();

// Use JobSharp
app.UseJobSharp();

app.Run();
```

## Configuration Examples

### Redis Standalone

```csharp
services.AddJobSharpRedis("localhost:6379");
```

### Redis with Authentication

```csharp
var configOptions = new ConfigurationOptions
{
    EndPoints = { "localhost:6379" },
    Password = "your-redis-password"
};
services.AddJobSharpRedis(configOptions);
```

### Redis Cluster

```csharp
var configOptions = new ConfigurationOptions
{
    EndPoints = {
        "redis-node1:6379",
        "redis-node2:6379",
        "redis-node3:6379"
    }
};
services.AddJobSharpRedis(configOptions);
```

### Redis Sentinel

```csharp
var configOptions = new ConfigurationOptions
{
    EndPoints = {
        "sentinel1:26379",
        "sentinel2:26379",
        "sentinel3:26379"
    },
    ServiceName = "mymaster",
    CommandMap = CommandMap.Sentinel
};
services.AddJobSharpRedis(configOptions);
```

## Job Data Format

### Job JSON Structure

```json
{
  "id": "job-id-guid",
  "typeName": "MyApp.Jobs.SendEmailJob",
  "arguments": "{\"email\":\"user@example.com\"}",
  "state": 1,
  "createdAt": "2024-01-01T00:00:00Z",
  "scheduledAt": "2024-01-01T01:00:00Z",
  "executedAt": null,
  "retryCount": 0,
  "maxRetryCount": 3,
  "errorMessage": null,
  "result": null,
  "batchId": "batch-id-guid",
  "parentJobId": "parent-job-id"
}
```

### Recurring Job JSON Structure

```json
{
  "id": "recurring-job-id",
  "cronExpression": "0 */5 * * * *",
  "jobTypeName": "MyApp.Jobs.CleanupJob",
  "jobArguments": null,
  "maxRetryCount": 3,
  "nextExecution": "2024-01-01T01:00:00Z",
  "lastExecution": "2024-01-01T00:55:00Z",
  "isEnabled": true,
  "createdAt": "2024-01-01T00:00:00Z"
}
```

## Performance Considerations

- **Memory Usage**: Redis stores all data in memory; monitor usage carefully
- **Persistence**: Configure Redis persistence (RDB/AOF) based on your durability needs
- **Connection Pooling**: StackExchange.Redis handles connection multiplexing automatically
- **Pipelining**: Multiple operations are naturally pipelined where possible

## Redis Configuration Recommendations

### For Job Processing Workloads

```
# redis.conf recommendations
maxmemory-policy allkeys-lru
save 900 1
save 300 10
save 60 10000
appendonly yes
appendfsync everysec
```

## Monitoring and Maintenance

### Redis Commands for Monitoring

```bash
# Check memory usage
INFO memory

# Monitor commands
MONITOR

# Check key count by pattern
KEYS jobsharp:*

# Get job counts by state
SCARD jobsharp:jobs:state:1  # Scheduled jobs
SCARD jobsharp:jobs:state:2  # Processing jobs
SCARD jobsharp:jobs:state:3  # Succeeded jobs
```

### Cleanup Operations

```bash
# Remove completed jobs (be careful with this!)
# This is just an example - implement proper cleanup logic
SCAN 0 MATCH jobsharp:jobs:* COUNT 1000
```

## High Availability Setup

### Redis Sentinel Configuration

```csharp
var configOptions = new ConfigurationOptions
{
    EndPoints = { "sentinel1:26379", "sentinel2:26379", "sentinel3:26379" },
    ServiceName = "mymaster",
    AbortOnConnectFail = false,
    ConnectTimeout = 5000,
    SyncTimeout = 5000
};

services.AddJobSharpRedis(configOptions);
```

### Redis Cluster Configuration

```csharp
var configOptions = new ConfigurationOptions
{
    EndPoints = { "node1:6379", "node2:6379", "node3:6379" },
    AbortOnConnectFail = false,
    ConnectTimeout = 5000,
    SyncTimeout = 5000
};

services.AddJobSharpRedis(configOptions);
```

## Troubleshooting

### Connection Issues
- Verify Redis server is running
- Check connection string format
- Ensure firewall rules allow connections
- Verify authentication credentials

### Performance Issues
- Monitor Redis memory usage with `INFO memory`
- Check for slow commands with `SLOWLOG GET`
- Consider using Redis pipelining for bulk operations
- Monitor network latency between application and Redis

### Memory Issues
- Set appropriate `maxmemory` and `maxmemory-policy`
- Implement job cleanup strategies
- Consider using Redis persistence for durability vs. memory trade-offs 