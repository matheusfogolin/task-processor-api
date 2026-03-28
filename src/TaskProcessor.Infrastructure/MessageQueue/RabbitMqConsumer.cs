using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TaskProcessor.Domain.Ports.MessageQueue;

namespace TaskProcessor.Infrastructure.MessageQueue;

public sealed class RabbitMqConsumer(
    IOptions<RabbitMqSettings> settings,
    ILogger<RabbitMqConsumer> logger)
    : IMessageQueueConsumer, IAsyncDisposable
{
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task ConsumeAsync<T>(
        Func<T, Task> handler,
        CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await InitializeAsync(ct);
                await ConsumeInternalAsync(handler, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Conexão com RabbitMQ perdida. Reconectando em 5 segundos. Queue={Queue}",
                    settings.Value.QueueName);

                await CleanupChannelAsync();
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private async Task ConsumeInternalAsync<T>(
        Func<T, Task> handler,
        CancellationToken ct)
    {
        await _channel!.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: ct);

        var channel = _channel!;
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            T message;
            try
            {
                var deserialized = JsonSerializer.Deserialize<T>(ea.Body.Span);
                if (deserialized is null)
                {
                    logger.LogError(
                        "Mensagem nula após deserialização. Queue={Queue} DeliveryTag={DeliveryTag}",
                        settings.Value.QueueName,
                        ea.DeliveryTag);

                    await channel.BasicNackAsync(
                        ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                message = deserialized;
            }
            catch (JsonException ex)
            {
                logger.LogError(
                    ex,
                    "Falha ao deserializar mensagem. Queue={Queue} DeliveryTag={DeliveryTag}",
                    settings.Value.QueueName,
                    ea.DeliveryTag);

                await channel.BasicNackAsync(
                    ea.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            try
            {
                await handler(message);
                await channel.BasicAckAsync(
                    ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Falha ao processar mensagem. Queue={Queue} DeliveryTag={DeliveryTag} Requeue={Requeue}",
                    settings.Value.QueueName,
                    ea.DeliveryTag,
                    !ea.Redelivered);

                await channel.BasicNackAsync(
                    ea.DeliveryTag, multiple: false, requeue: !ea.Redelivered);
            }
        };

        await channel.BasicConsumeAsync(
            queue: settings.Value.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        var tcs = new TaskCompletionSource();
        using (ct.Register(() => tcs.TrySetResult()))
            await tcs.Task;
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        await CleanupChannelAsync();

        var factory = new ConnectionFactory
        {
            HostName = settings.Value.HostName,
            Port = settings.Value.Port,
            UserName = settings.Value.UserName,
            Password = settings.Value.Password
        };

        logger.LogInformation(
            "Conectando ao RabbitMQ em {Host}:{Port}. Queue={Queue}",
            settings.Value.HostName,
            settings.Value.Port,
            settings.Value.QueueName);

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        await _channel.QueueDeclareAsync(
            queue: settings.Value.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);
    }

    private async Task CleanupChannelAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync() =>
        await CleanupChannelAsync();
}
