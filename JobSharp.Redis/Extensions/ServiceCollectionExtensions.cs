using JobSharp.Redis.Storage;
using JobSharp.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace JobSharp.Redis.Extensions;

/// <summary>
/// Extension methods for configuring Redis storage for JobSharp.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Redis storage for JobSharp.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <param name="database">The Redis database number (default: 0).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpRedis(this IServiceCollection services,
        string connectionString,
        int database = 0)
    {
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.TryAddScoped<IDatabase>(serviceProvider =>
        {
            var multiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
            return multiplexer.GetDatabase(database);
        });
        services.TryAddScoped<IJobStorage, RedisJobStorage>();

        return services;
    }

    /// <summary>
    /// Adds Redis storage for JobSharp with custom configuration options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configurationOptions">The Redis configuration options.</param>
    /// <param name="database">The Redis database number (default: 0).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpRedis(this IServiceCollection services,
        ConfigurationOptions configurationOptions,
        int database = 0)
    {
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configurationOptions));
        services.TryAddScoped<IDatabase>(serviceProvider =>
        {
            var multiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
            return multiplexer.GetDatabase(database);
        });
        services.TryAddScoped<IJobStorage, RedisJobStorage>();

        return services;
    }

    /// <summary>
    /// Adds Redis storage for JobSharp with a custom database factory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databaseFactory">A factory function to create the Redis database.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpRedis(this IServiceCollection services,
        Func<IServiceProvider, IDatabase> databaseFactory)
    {
        services.TryAddScoped(databaseFactory);
        services.TryAddScoped<IJobStorage, RedisJobStorage>();

        return services;
    }
}