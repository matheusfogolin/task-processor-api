using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Domain.Ports.MessageQueue;
using TaskProcessor.Infrastructure.MessageQueue;
using TaskProcessor.Infrastructure.Persistence;
using TaskProcessor.Infrastructure.Repositories;

namespace TaskProcessor.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(
            configuration.GetSection("MongoDB"));

        services.Configure<RabbitMqSettings>(
            configuration.GetSection("RabbitMQ"));

        services.AddSingleton<MongoDbContext>();
        services.AddSingleton<IJobRepository, JobRepository>();
        services.AddSingleton<IMessageQueuePublisher, RabbitMqPublisher>();
        services.AddSingleton<IMessageQueueConsumer, RabbitMqConsumer>();
        services.AddSingleton<RabbitMqTopologyInitializer>();

        return services;
    }

    public static async Task InitializeInfrastructureAsync(
        this IServiceProvider serviceProvider,
        CancellationToken ct = default)
    {
        var context = serviceProvider.GetRequiredService<MongoDbContext>();
        await context.EnsureIndexesCreatedAsync(ct);

        var topologyInitializer = serviceProvider.GetRequiredService<RabbitMqTopologyInitializer>();
        await topologyInitializer.InitializeAsync(ct);
    }
}
