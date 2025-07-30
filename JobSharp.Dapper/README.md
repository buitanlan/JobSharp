# JobSharp.Dapper

A Dapper-based storage provider for JobSharp that supports multiple database backends including SQL Server, PostgreSQL, MySQL, and SQLite.

## Features

- **High Performance**: Uses Dapper for fast, lightweight database operations
- **Multi-Database Support**: Works with SQL Server, PostgreSQL, MySQL, and SQLite
- **Flexible Connection Management**: Support for custom connection factories
- **Database Schema Scripts**: Includes ready-to-use schema scripts for all supported databases

## Installation

```bash
dotnet add package JobSharp.Dapper
```

## Quick Start

### SQL Server

```csharp
using JobSharp.Dapper.Extensions;

services.AddJobSharpDapper("Server=localhost;Database=JobSharp;Trusted_Connection=true;");
```

### SQLite

```csharp
using JobSharp.Dapper.Extensions;

services.AddJobSharpDapperSqlite("Data Source=jobsharp.db");
```

### PostgreSQL

```csharp
using JobSharp.Dapper.Extensions;

services.AddJobSharpDapperPostgreSQL("Host=localhost;Database=jobsharp;Username=user;Password=password");
```

### MySQL

```csharp
using JobSharp.Dapper.Extensions;

services.AddJobSharpDapperMySQL("Server=localhost;Database=jobsharp;Uid=user;Pwd=password;");
```

### Custom Connection Factory

```csharp
using JobSharp.Dapper.Extensions;

services.AddJobSharpDapper(serviceProvider => 
{
    var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
        .GetConnectionString("JobSharp");
    return new SqlConnection(connectionString);
});
```

## Database Setup

Before using JobSharp.Dapper, you need to create the required database tables. Use the appropriate schema script from the `Scripts` folder:

- **SQL Server**: `Scripts/SqlServer.sql`
- **SQLite**: `Scripts/SQLite.sql`
- **PostgreSQL**: `Scripts/PostgreSQL.sql`
- **MySQL**: `Scripts/MySQL.sql`

### Example: Creating SQLite Database

```csharp
using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;

// Create connection and execute schema
using var connection = new SqliteConnection("Data Source=jobsharp.db");
connection.Open();

var schemaScript = File.ReadAllText("Scripts/SQLite.sql");
await connection.ExecuteAsync(schemaScript);
```

## Complete Example

```csharp
using JobSharp.Extensions;
using JobSharp.Dapper.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add JobSharp services
builder.Services.AddJobSharp();

// Add Dapper storage
builder.Services.AddJobSharpDapperSqlite("Data Source=jobsharp.db");

var app = builder.Build();

// Use JobSharp
app.UseJobSharp();

app.Run();
```

## Schema Tables

The Dapper provider creates two main tables:

### Jobs Table
Stores individual job instances with their state, arguments, and execution information.

### RecurringJobs Table
Stores recurring job schedules with cron expressions and job templates.

Both tables include appropriate indexes for optimal query performance.

## Connection Management

The Dapper implementation uses `IDbConnection` which is registered as scoped in the DI container. Connections are automatically managed and disposed by the DI container.

## Database Compatibility

- **SQL Server**: Full support with OFFSET/FETCH pagination
- **PostgreSQL**: Full support with LIMIT pagination
- **MySQL**: Full support with LIMIT pagination  
- **SQLite**: Full support with LIMIT pagination

## Performance Considerations

- All queries use parameterized SQL to prevent SQL injection
- Indexes are created on frequently queried columns
- Batch operations are supported for bulk inserts
- Connection pooling is handled by the underlying database provider

## Troubleshooting

### Connection Issues
Make sure your connection string is correct and the database is accessible.

### Schema Issues
Ensure the database schema has been created using the appropriate SQL script.

### Performance Issues
Check that the database indexes are properly created and consider adjusting batch sizes for your workload. 