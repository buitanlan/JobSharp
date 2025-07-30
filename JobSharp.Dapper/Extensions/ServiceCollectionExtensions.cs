using System.Data;
using JobSharp.Dapper.Storage;
using JobSharp.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;
using Npgsql;

namespace JobSharp.Dapper.Extensions;

/// <summary>
/// Extension methods for configuring Dapper storage for JobSharp.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapper storage for JobSharp using SQL Server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpDapper(this IServiceCollection services, string connectionString)
    {
        services.TryAddScoped<IDbConnection>(_ => new SqlConnection(connectionString));
        services.TryAddScoped<IJobStorage, DapperJobStorage>();
        return services;
    }

    /// <summary>
    /// Adds Dapper storage for JobSharp using SQLite.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpDapperSqlite(this IServiceCollection services, string connectionString)
    {
        services.TryAddScoped<IDbConnection>(_ => new SqliteConnection(connectionString));
        services.TryAddScoped<IJobStorage, DapperJobStorage>();
        return services;
    }

    /// <summary>
    /// Adds Dapper storage for JobSharp using PostgreSQL.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpDapperPostgreSQL(this IServiceCollection services, string connectionString)
    {
        services.TryAddScoped<IDbConnection>(_ => new NpgsqlConnection(connectionString));
        services.TryAddScoped<IJobStorage, DapperJobStorage>();
        return services;
    }

    /// <summary>
    /// Adds Dapper storage for JobSharp using MySQL.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The MySQL connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpDapperMySQL(this IServiceCollection services, string connectionString)
    {
        services.TryAddScoped<IDbConnection>(_ => new MySqlConnection(connectionString));
        services.TryAddScoped<IJobStorage, DapperJobStorage>();
        return services;
    }

    /// <summary>
    /// Adds Dapper storage for JobSharp with a custom IDbConnection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionFactory">A factory function to create the database connection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpDapper(this IServiceCollection services, Func<IServiceProvider, IDbConnection> connectionFactory)
    {
        services.TryAddScoped(connectionFactory);
        services.TryAddScoped<IJobStorage, DapperJobStorage>();
        return services;
    }
} 