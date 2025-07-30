using JobSharp.Core;
using JobSharp.Processing;
using JobSharp.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace JobSharp.Extensions;

/// <summary>
/// Extension methods for configuring JobSharp services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds JobSharp services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional configuration for job processor options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobSharp(this IServiceCollection services, Action<JobProcessorOptions>? configureOptions = null)
    {
        // Register core services
        services.TryAddScoped<IJobClient, JobClient>();
        services.TryAddSingleton<IJobProcessor, JobProcessor>();

        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<JobProcessorOptions>(_ => { });
        }

        // Register the hosted service for background processing
        services.AddHostedService<JobProcessorHostedService>();

        return services;
    }

    /// <summary>
    /// Adds a job handler to the service collection.
    /// </summary>
    /// <typeparam name="TJob">The type of job the handler processes.</typeparam>
    /// <typeparam name="THandler">The type of the job handler.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobHandler<TJob, THandler>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TJob : class
        where THandler : class, IJobHandler<TJob>
    {
        services.Add(new ServiceDescriptor(typeof(IJobHandler<TJob>), typeof(THandler), lifetime));
        services.Add(new ServiceDescriptor(typeof(IJobHandler), typeof(THandler), lifetime));
        services.Add(new ServiceDescriptor(typeof(THandler), typeof(THandler), lifetime));

        return services;
    }

    /// <summary>
    /// Adds a job handler instance to the service collection.
    /// </summary>
    /// <typeparam name="TJob">The type of job the handler processes.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="handler">The job handler instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobHandler<TJob>(this IServiceCollection services, IJobHandler<TJob> handler)
        where TJob : class
    {
        services.AddSingleton<IJobHandler<TJob>>(_ => handler);
        services.AddSingleton<IJobHandler>(_ => (IJobHandler)handler);

        return services;
    }

    /// <summary>
    /// Scans assemblies for job handlers and registers them automatically.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan. If null, scans the calling assembly.</param>
    /// <param name="lifetime">The service lifetime for discovered handlers.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobHandlers(this IServiceCollection services,
        System.Reflection.Assembly[]? assemblies = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        assemblies ??= [System.Reflection.Assembly.GetCallingAssembly()];

        foreach (var assembly in assemblies)
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IJobHandler<>)))
                .ToList();

            foreach (var handlerType in handlerTypes)
            {
                var jobHandlerInterface = handlerType.GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IJobHandler<>));

                services.Add(new ServiceDescriptor(jobHandlerInterface, handlerType, lifetime));
                services.Add(new ServiceDescriptor(typeof(IJobHandler), handlerType, lifetime));
                services.Add(new ServiceDescriptor(handlerType, handlerType, lifetime));
            }
        }

        return services;
    }
}

/// <summary>
/// Hosted service that manages the job processor lifecycle.
/// </summary>
internal class JobProcessorHostedService : BackgroundService
{
    private readonly IJobProcessor _jobProcessor;

    public JobProcessorHostedService(IJobProcessor jobProcessor)
    {
        _jobProcessor = jobProcessor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _jobProcessor.StartAsync(stoppingToken);

        // Keep the service running until cancellation is requested
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopping
        }
        finally
        {
            await _jobProcessor.StopAsync(CancellationToken.None);
        }
    }
}