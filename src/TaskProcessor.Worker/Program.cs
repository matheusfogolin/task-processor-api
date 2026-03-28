using TaskProcessor.Infrastructure;
using TaskProcessor.Worker.Consumers;
using TaskProcessor.Worker.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddInfrastructure(context.Configuration);
        services.AddHostedService<JobCreationConsumer>();
        services.AddHostedService<JobProcessingService>();
    })
    .Build();

await host.Services.InitializeInfrastructureAsync();
await host.RunAsync();
