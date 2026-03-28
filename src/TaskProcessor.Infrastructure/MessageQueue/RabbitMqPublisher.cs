using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TaskProcessor.Domain.Ports.MessageQueue;

namespace TaskProcessor.Infrastructure.MessageQueue;

public sealed class RabbitMqPublisher(
    IOptions<RabbitMqSettings> settings,
    ILogger<RabbitMqPublisher> logger)
    : IMessageQueuePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync<T>(T message, CancellationToken ct = default)
    {
        var channel = await EnsureChannelCreatedAsync(ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: settings.Value.QueueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);
    }

    private async Task<IChannel> EnsureChannelCreatedAsync(CancellationToken ct)
    {
        if (_channel is not null && _channel.IsOpen)
            return _channel;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_channel is not null && _channel.IsOpen)
                return _channel;

            await ResetStaleResourcesAsync();

            var factory = new ConnectionFactory
            {
                HostName = settings.Value.HostName,
                Port = settings.Value.Port,
                UserName = settings.Value.UserName,
                Password = settings.Value.Password
            };

            logger.LogInformation(
                "Abrindo conexão com RabbitMQ em {Host}:{Port}.",
                settings.Value.HostName,
                settings.Value.Port);

            _connection = await factory.CreateConnectionAsync(ct);
            _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

            await _channel.QueueDeclareAsync(
                queue: settings.Value.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct);

            return _channel;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task ResetStaleResourcesAsync()
    {
        if (_channel is not null)
        {
            logger.LogWarning("Canal RabbitMQ fechado. Descartando e reconectando.");
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null && !_connection.IsOpen)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();

        _initLock.Dispose();
    }
}
