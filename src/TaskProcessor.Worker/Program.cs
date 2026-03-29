using TaskProcessor.Infrastructure;
using TaskProcessor.Worker.Consumers;
using TaskProcessor.Worker.Services;
using TaskProcessor.Worker.Settings;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddInfrastructure(context.Configuration);
        services.Configure<JobProcessingSettings>(context.Configuration.GetSection("JobProcessing"));
        services.AddHostedService<JobCreationConsumer>();
        services.AddHostedService<JobProcessingService>();
    })
    .Build();

await host.Services.InitializeInfrastructureAsync();
await host.RunAsync();
