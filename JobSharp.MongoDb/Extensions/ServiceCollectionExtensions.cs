using JobSharp.MongoDb.Storage;
using JobSharp.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;

namespace JobSharp.MongoDb.Extensions;

/// <summary>
/// Extension methods for configuring MongoDB storage for JobSharp.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MongoDB storage for JobSharp.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The MongoDB connection string.</param>
    /// <param name="databaseName">The name of the MongoDB database.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpMongoDb(this IServiceCollection services,
        string connectionString,
        string databaseName)
    {
        services.TryAddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.TryAddScoped<IMongoDatabase>(serviceProvider =>
        {
            var client = serviceProvider.GetRequiredService<IMongoClient>();
            return client.GetDatabase(databaseName);
        });
        services.TryAddScoped<IJobStorage, MongoDbJobStorage>();

        return services;
    }

    /// <summary>
    /// Adds MongoDB storage for JobSharp with custom client settings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="clientSettings">The MongoDB client settings.</param>
    /// <param name="databaseName">The name of the MongoDB database.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpMongoDb(this IServiceCollection services,
        MongoClientSettings clientSettings,
        string databaseName)
    {
        services.TryAddSingleton<IMongoClient>(_ => new MongoClient(clientSettings));
        services.TryAddScoped<IMongoDatabase>(serviceProvider =>
        {
            var client = serviceProvider.GetRequiredService<IMongoClient>();
            return client.GetDatabase(databaseName);
        });
        services.TryAddScoped<IJobStorage, MongoDbJobStorage>();

        return services;
    }

    /// <summary>
    /// Adds MongoDB storage for JobSharp with a custom database factory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databaseFactory">A factory function to create the MongoDB database.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpMongoDb(this IServiceCollection services,
        Func<IServiceProvider, IMongoDatabase> databaseFactory)
    {
        services.TryAddScoped(databaseFactory);
        services.TryAddScoped<IJobStorage, MongoDbJobStorage>();

        return services;
    }
}