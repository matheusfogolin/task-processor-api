using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace TaskProcessor.Tests.Infrastructure;

public class RabbitMqPublisherRetryPolicyTest
{
    private static ResiliencePipeline CreateRetryPipeline(int maxRetries) =>
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
                UseJitter = false,
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.Zero
            })
            .Build();

    [Fact]
    public async Task PublishAsync_FirstAttemptSucceeds_DoesNotRetry()
    {
        var pipeline = CreateRetryPipeline(maxRetries: 3);
        var callCount = 0;

        await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            return ValueTask.CompletedTask;
        });

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_TransientFailureThenSuccess_RetriesAndSucceeds()
    {
        var pipeline = CreateRetryPipeline(maxRetries: 3);
        var callCount = 0;
        var shutdownArgs = new ShutdownEventArgs(ShutdownInitiator.Library, 0, "test");

        await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            if (callCount == 1)
                throw new AlreadyClosedException(shutdownArgs);

            return ValueTask.CompletedTask;
        });

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task PublishAsync_AllRetriesExhausted_ThrowsLastException()
    {
        var pipeline = CreateRetryPipeline(maxRetries: 2);
        var callCount = 0;
        var shutdownArgs = new ShutdownEventArgs(ShutdownInitiator.Library, 0, "test");

        var act = async () => await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            throw new AlreadyClosedException(shutdownArgs);
        });

        await act.Should().ThrowAsync<AlreadyClosedException>();
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task PublishAsync_NonRetryableException_ThrowsImmediately()
    {
        var pipeline = CreateRetryPipeline(maxRetries: 3);
        var callCount = 0;

        var act = async () => await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            throw new InvalidOperationException("not retryable");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_OperationInterruptedException_Retries()
    {
        var pipeline = CreateRetryPipeline(maxRetries: 3);
        var callCount = 0;
        var shutdownArgs = new ShutdownEventArgs(ShutdownInitiator.Library, 0, "test");

        await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            if (callCount == 1)
                throw new OperationInterruptedException(shutdownArgs);

            return ValueTask.CompletedTask;
        });

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task PublishAsync_BrokerUnreachableException_Retries()
    {
        var pipeline = CreateRetryPipeline(maxRetries: 3);
        var callCount = 0;

        await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            if (callCount == 1)
                throw new BrokerUnreachableException(new Exception("test"));

            return ValueTask.CompletedTask;
        });

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task PublishAsync_IOException_Retries()
    {
        var pipeline = CreateRetryPipeline(maxRetries: 3);
        var callCount = 0;

        await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            if (callCount == 1)
                throw new IOException("connection reset");

            return ValueTask.CompletedTask;
        });

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task PublishAsync_SocketException_Retries()
    {
        var pipeline = CreateRetryPipeline(maxRetries: 3);
        var callCount = 0;

        await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            if (callCount == 1)
                throw new SocketException((int)SocketError.ConnectionRefused);

            return ValueTask.CompletedTask;
        });

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task PublishAsync_MultipleConsecutiveFailuresThenSuccess_RetriesUntilSuccess()
    {
        var pipeline = CreateRetryPipeline(maxRetries: 3);
        var callCount = 0;
        var shutdownArgs = new ShutdownEventArgs(ShutdownInitiator.Library, 0, "test");

        await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            if (callCount <= 3)
                throw new AlreadyClosedException(shutdownArgs);

            return ValueTask.CompletedTask;
        });

        callCount.Should().Be(4);
    }

    [Fact]
    public async Task PublishAsync_MixedRetryableExceptions_RetriesAll()
    {
        var pipeline = CreateRetryPipeline(maxRetries: 3);
        var callCount = 0;
        var shutdownArgs = new ShutdownEventArgs(ShutdownInitiator.Library, 0, "test");

        await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            if (callCount == 1)
                throw new AlreadyClosedException(shutdownArgs);
            if (callCount == 2)
                throw new BrokerUnreachableException(new Exception("test"));

            return ValueTask.CompletedTask;
        });

        callCount.Should().Be(3);
    }

    [Fact]
    public void PublishAsync_JsonException_IsNotRetryable()
    {
        var pipeline = CreateRetryPipeline(maxRetries: 3);
        var callCount = 0;

        var act = async () => await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            throw new JsonException("invalid json");
        });

        act.Should().ThrowAsync<JsonException>();
        callCount.Should().Be(1);
    }
}
