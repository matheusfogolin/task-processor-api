using System.Text;
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
                    logger.LogWarning(
                        "Mensagem nula após deserialização. Queue={Queue} DeliveryTag={DeliveryTag}",
                        settings.Value.QueueName,
                        ea.DeliveryTag);

                    await PublishToDeadLetterAsync(channel, ea.Body, ea.BasicProperties, CancellationToken.None);
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    return;
                }

                message = deserialized;
            }
            catch (JsonException ex)
            {
                logger.LogWarning(
                    ex,
                    "Falha ao deserializar mensagem. Queue={Queue} DeliveryTag={DeliveryTag}",
                    settings.Value.QueueName,
                    ea.DeliveryTag);

                await PublishToDeadLetterAsync(channel, ea.Body, ea.BasicProperties, CancellationToken.None);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                return;
            }

            try
            {
                await handler(message);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                var retryCount = GetRetryCount(ea.BasicProperties, settings.Value.QueueName);

                if (retryCount < settings.Value.MaxRetries)
                {
                    logger.LogError(
                        ex,
                        "Falha ao processar mensagem. Enviando para retry. Queue={Queue} DeliveryTag={DeliveryTag} RetryCount={RetryCount}",
                        settings.Value.QueueName,
                        ea.DeliveryTag,
                        retryCount);

                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                logger.LogWarning(
                    ex,
                    "Mensagem excedeu limite de retries. Enviando para deadletter. Queue={Queue} DeliveryTag={DeliveryTag} RetryCount={RetryCount}",
                    settings.Value.QueueName,
                    ea.DeliveryTag,
                    retryCount);

                await PublishToDeadLetterAsync(channel, ea.Body, ea.BasicProperties, CancellationToken.None);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
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

    internal static int GetRetryCount(IReadOnlyBasicProperties properties, string queueName)
    {
        if (properties.Headers is null)
            return 0;

        if (!properties.Headers.TryGetValue("x-death", out var xDeathRaw))
            return 0;

        if (xDeathRaw is not List<object> xDeath)
            return 0;

        foreach (var entry in xDeath)
        {
            if (entry is not Dictionary<string, object> dict)
                continue;

            var queue = dict.TryGetValue("queue", out var q)
                ? q is byte[] qBytes ? Encoding.UTF8.GetString(qBytes) : q as string
                : null;

            var reason = dict.TryGetValue("reason", out var r)
                ? r is byte[] rBytes ? Encoding.UTF8.GetString(rBytes) : r as string
                : null;

            if (queue != queueName || reason != "rejected")
                continue;

            if (!dict.TryGetValue("count", out var countObj))
                return 1;

            return countObj switch
            {
                long l => (int)l,
                int i => i,
                _ => 1
            };
        }

        return 0;
    }

    private async Task PublishToDeadLetterAsync(
        IChannel channel,
        ReadOnlyMemory<byte> body,
        IReadOnlyBasicProperties originalProperties,
        CancellationToken ct)
    {
        var deadLetterExchange = $"{settings.Value.QueueName}-deadletter";

        var properties = new BasicProperties
        {
            Persistent = true
        };

        if (originalProperties.Headers is not null)
            properties.Headers = new Dictionary<string, object?>(originalProperties.Headers);

        await channel.BasicPublishAsync(
            exchange: deadLetterExchange,
            routingKey: deadLetterExchange,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);
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
