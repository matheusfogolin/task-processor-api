using System.Text.Json;
using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using TaskProcessor.Domain.Ports.MessageQueue;

namespace TaskProcessor.Infrastructure.MessageQueue;

public sealed class RabbitMqPublisher(
    IOptions<RabbitMqSettings> settings,
    ILogger<RabbitMqPublisher> logger)
    : IMessageQueuePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly Lazy<ResiliencePipeline> _retryPipeline = new(() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<AlreadyClosedException>()
                    .Handle<OperationInterruptedException>()
                    .Handle<BrokerUnreachableException>()
                    .Handle<IOException>()
                    .Handle<SocketException>(),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxRetryAttempts = settings.Value.PublishMaxRetries,
                Delay = TimeSpan.FromMilliseconds(settings.Value.PublishRetryBaseDelayMs),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Tentativa de publicação {AttemptNumber} falhou. Retentando em {RetryDelay}. Erro: {ExceptionMessage}",
                        args.AttemptNumber + 1,
                        args.RetryDelay,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build());

    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync<T>(T message, CancellationToken ct = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await _retryPipeline.Value.ExecuteAsync(async token =>
        {
            var channel = await EnsureChannelCreatedAsync(token);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await channel.BasicPublishAsync(
                exchange: settings.Value.QueueName,
                routingKey: settings.Value.QueueName,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: token);
        }, ct);
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

        if (_connection is not null)
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
