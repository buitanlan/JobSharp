# JobSharp.MongoDb

A MongoDB-based storage provider for JobSharp that leverages MongoDB's document-oriented database for job storage and processing.

## Features

- **Document-Based Storage**: Native MongoDB document storage with BSON serialization
- **Automatic Indexing**: Creates optimal indexes for job queries automatically
- **Flexible Configuration**: Multiple ways to configure MongoDB connection
- **High Performance**: Efficient queries using MongoDB's native aggregation pipeline
- **Scalable**: Supports MongoDB's horizontal scaling capabilities

## Installation

```bash
dotnet add package JobSharp.MongoDb
```

## Quick Start

### Basic Configuration

```csharp
using JobSharp.MongoDb.Extensions;

services.AddJobSharpMongoDb("mongodb://localhost:27017", "jobsharp");
```

### With Custom Client Settings

```csharp
using JobSharp.MongoDb.Extensions;
using MongoDB.Driver;

var settings = new MongoClientSettings
{
    Server = new MongoServerAddress("localhost", 27017),
    ConnectTimeout = TimeSpan.FromSeconds(30)
};

services.AddJobSharpMongoDb(settings, "jobsharp");
```

### Custom Database Factory

```csharp
using JobSharp.MongoDb.Extensions;

services.AddJobSharpMongoDb(serviceProvider =>
{
    var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
        .GetConnectionString("MongoDB");
    var client = new MongoClient(connectionString);
    return client.GetDatabase("jobsharp");
});
```

## Database Setup

JobSharp.MongoDb automatically creates the necessary collections and indexes when the storage is first used:

### Collections

- **jobs**: Main collection storing job documents
- **recurringJobs**: Collection for recurring job schedules

### Indexes

The following indexes are automatically created for optimal performance:

```javascript
// Jobs collection indexes
db.jobs.createIndex({ "state": 1 })
db.jobs.createIndex({ "scheduledAt": 1 })
db.jobs.createIndex({ "batchId": 1 })
db.jobs.createIndex({ "parentJobId": 1 })
db.jobs.createIndex({ "state": 1, "scheduledAt": 1 })

// Recurring jobs collection indexes
db.recurringJobs.createIndex({ "isEnabled": 1 })
db.recurringJobs.createIndex({ "nextExecution": 1 })
```

## Complete Example

```csharp
using JobSharp.Extensions;
using JobSharp.MongoDb.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add JobSharp services
builder.Services.AddJobSharp();

// Add MongoDB storage
builder.Services.AddJobSharpMongoDb("mongodb://localhost:27017", "jobsharp");

var app = builder.Build();

// Use JobSharp
app.UseJobSharp();

app.Run();
```

## Configuration Options

### Connection String Examples

```csharp
// Local MongoDB
services.AddJobSharpMongoDb("mongodb://localhost:27017", "jobsharp");

// MongoDB with authentication
services.AddJobSharpMongoDb("mongodb://username:password@localhost:27017", "jobsharp");

// MongoDB Atlas
services.AddJobSharpMongoDb("mongodb+srv://user:pass@cluster.mongodb.net", "jobsharp");

// Replica Set
services.AddJobSharpMongoDb("mongodb://host1:27017,host2:27017,host3:27017/?replicaSet=myReplSet", "jobsharp");
```

## Document Structure

### Job Document

```json
{
  "_id": "job-id-guid",
  "typeName": "MyApp.Jobs.SendEmailJob",
  "arguments": "{\"email\":\"user@example.com\"}",
  "state": 1,
  "createdAt": ISODate("2024-01-01T00:00:00Z"),
  "scheduledAt": ISODate("2024-01-01T01:00:00Z"),
  "executedAt": null,
  "retryCount": 0,
  "maxRetryCount": 3,
  "errorMessage": null,
  "result": null,
  "batchId": "batch-id-guid",
  "parentJobId": "parent-job-id"
}
```

### Recurring Job Document

```json
{
  "_id": "recurring-job-id",
  "cronExpression": "0 */5 * * * *",
  "jobTypeName": "MyApp.Jobs.CleanupJob",
  "jobArguments": null,
  "maxRetryCount": 3,
  "nextExecution": ISODate("2024-01-01T01:00:00Z"),
  "lastExecution": ISODate("2024-01-01T00:55:00Z"),
  "isEnabled": true,
  "createdAt": ISODate("2024-01-01T00:00:00Z")
}
```

## Performance Considerations

- **Sharding**: For large-scale deployments, consider sharding the jobs collection by job ID
- **Indexes**: The provider creates optimal indexes, but monitor query performance
- **TTL**: Consider adding TTL indexes for automatic cleanup of completed jobs
- **Connection Pooling**: MongoDB driver handles connection pooling automatically

## Monitoring and Maintenance

### Query Performance

```javascript
// Check index usage
db.jobs.find({state: 1, scheduledAt: {$lte: new Date()}}).explain("executionStats")

// Monitor slow queries
db.setProfilingLevel(2, {slowms: 100})
```

### Cleanup Operations

```javascript
// Remove completed jobs older than 30 days
db.jobs.deleteMany({
  state: 3, // Succeeded
  executedAt: {$lt: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)}
})
```

## Troubleshooting

### Connection Issues
- Verify MongoDB connection string
- Check network connectivity
- Ensure MongoDB service is running

### Performance Issues
- Monitor index usage with `.explain()`
- Consider query optimization
- Check MongoDB server resources

### Document Size Limits
- BSON document size limit is 16MB
- Large job arguments should be stored externally if needed 