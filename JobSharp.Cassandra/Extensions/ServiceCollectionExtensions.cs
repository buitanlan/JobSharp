using Cassandra;
using JobSharp.Cassandra.Storage;
using JobSharp.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobSharp.Cassandra.Extensions;

/// <summary>
/// Extension methods for configuring Cassandra storage for JobSharp.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Cassandra storage for JobSharp.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="contactPoints">The Cassandra contact points.</param>
    /// <param name="keyspace">The keyspace name.</param>
    /// <param name="port">The port number (default: 9042).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpCassandra(this IServiceCollection services,
        string[] contactPoints,
        string keyspace,
        int port = 9042)
    {
        services.TryAddSingleton<ICluster>(_ =>
        {
            var cluster = Cluster.Builder()
                .AddContactPoints(contactPoints)
                .WithPort(port)
                .Build();
            return cluster;
        });

        services.TryAddScoped<ISession>(serviceProvider =>
        {
            var cluster = serviceProvider.GetRequiredService<ICluster>();
            return cluster.Connect(keyspace);
        });

        services.TryAddScoped<IJobStorage, CassandraJobStorage>();

        return services;
    }

    /// <summary>
    /// Adds Cassandra storage for JobSharp with custom cluster configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="clusterBuilder">A function to configure the cluster builder.</param>
    /// <param name="keyspace">The keyspace name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpCassandra(this IServiceCollection services,
        Func<Builder, Builder> clusterBuilder,
        string keyspace)
    {
        services.TryAddSingleton<ICluster>(_ =>
        {
            var builder = Cluster.Builder();
            builder = clusterBuilder(builder);
            return builder.Build();
        });

        services.TryAddScoped<ISession>(serviceProvider =>
        {
            var cluster = serviceProvider.GetRequiredService<ICluster>();
            return cluster.Connect(keyspace);
        });

        services.TryAddScoped<IJobStorage, CassandraJobStorage>();

        return services;
    }

    /// <summary>
    /// Adds Cassandra storage for JobSharp with a custom session factory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="sessionFactory">A factory function to create the Cassandra session.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharpCassandra(this IServiceCollection services,
        Func<IServiceProvider, ISession> sessionFactory)
    {
        services.TryAddScoped(sessionFactory);
        services.TryAddScoped<IJobStorage, CassandraJobStorage>();

        return services;
    }
}