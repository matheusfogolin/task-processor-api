using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace TaskProcessor.Infrastructure.MessageQueue;

public sealed class RabbitMqTopologyInitializer(
    IOptions<RabbitMqSettings> options,
    ILogger<RabbitMqTopologyInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var queueName = settings.QueueName;

        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password
        };

        logger.LogInformation(
            "Iniciando declaração de topologia RabbitMQ para fila '{QueueName}'.",
            queueName);

        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await DeclareExchangesAsync(channel, queueName, ct);
        await DeclareQueuesAsync(channel, queueName, settings.RetryTtlMs, ct);
        await DeclareBindingsAsync(channel, queueName, ct);

        logger.LogInformation(
            "Topologia RabbitMQ declarada com sucesso para fila '{QueueName}'.",
            queueName);
    }

    private static async Task DeclareExchangesAsync(IChannel channel, string queueName, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            exchange: queueName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            exchange: $"{queueName}-retry",
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            exchange: $"{queueName}-deadletter",
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);
    }

    private static async Task DeclareQueuesAsync(IChannel channel, string queueName, int retryTtlMs, CancellationToken ct)
    {
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                [Headers.XDeadLetterExchange] = $"{queueName}-retry",
                [Headers.XDeadLetterRoutingKey] = $"{queueName}-retry"
            },
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue: $"{queueName}-retry",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                [Headers.XDeadLetterExchange] = queueName,
                [Headers.XDeadLetterRoutingKey] = queueName,
                [Headers.XMessageTTL] = (int)retryTtlMs
            },
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue: $"{queueName}-deadletter",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);
    }

    private static async Task DeclareBindingsAsync(IChannel channel, string queueName, CancellationToken ct)
    {
        await channel.QueueBindAsync(
            queue: queueName,
            exchange: queueName,
            routingKey: queueName,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: $"{queueName}-retry",
            exchange: $"{queueName}-retry",
            routingKey: $"{queueName}-retry",
            arguments: null,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: $"{queueName}-deadletter",
            exchange: $"{queueName}-deadletter",
            routingKey: $"{queueName}-deadletter",
            arguments: null,
            cancellationToken: ct);
    }
}
