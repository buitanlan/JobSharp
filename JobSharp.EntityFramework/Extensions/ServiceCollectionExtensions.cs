using JobSharp.EntityFramework.Storage;
using JobSharp.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobSharp.EntityFramework.Extensions;

/// <summary>
/// Extension methods for configuring Entity Framework storage for JobSharp.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Entity Framework storage for JobSharp using SQL Server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <param name="configureOptions">Optional configuration for DbContext options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpEntityFramework(this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configureOptions = null)
    {
        services.AddDbContext<JobSharpDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
            configureOptions?.Invoke(options);
        });

        services.TryAddScoped<IJobStorage, EntityFrameworkJobStorage>();

        return services;
    }

    /// <summary>
    /// Adds Entity Framework storage for JobSharp using SQLite.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="configureOptions">Optional configuration for DbContext options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpEntityFrameworkSqlite(this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configureOptions = null)
    {
        services.AddDbContext<JobSharpDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            configureOptions?.Invoke(options);
        });

        services.TryAddScoped<IJobStorage, EntityFrameworkJobStorage>();

        return services;
    }

    /// <summary>
    /// Adds Entity Framework storage for JobSharp with custom DbContext configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureDbContext">Configuration action for the DbContext.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpEntityFramework(this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<JobSharpDbContext>(configureDbContext);
        services.TryAddScoped<IJobStorage, EntityFrameworkJobStorage>();

        return services;
    }

    /// <summary>
    /// Ensures the JobSharp database is created and up to date.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task EnsureJobSharpDatabaseCreatedAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Migrates the JobSharp database to the latest version.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task MigrateJobSharpDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobSharpDbContext>();
        await context.Database.MigrateAsync();
    }
}