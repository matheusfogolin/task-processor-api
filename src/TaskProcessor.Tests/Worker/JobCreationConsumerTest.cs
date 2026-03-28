using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaskProcessor.Application.Shared.Messages;
using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Domain.Ports.MessageQueue;
using TaskProcessor.Worker.Consumers;

namespace TaskProcessor.Tests.Worker;

public class JobCreationConsumerTest
{
    private readonly Mock<IMessageQueueConsumer> _consumerMock;
    private readonly Mock<IJobRepository> _repositoryMock;
    private readonly Mock<ILogger<JobCreationConsumer>> _loggerMock;
    private readonly JobCreationConsumer _sut;

    public JobCreationConsumerTest()
    {
        _consumerMock = new Mock<IMessageQueueConsumer>();
        _repositoryMock = new Mock<IJobRepository>();
        _loggerMock = new Mock<ILogger<JobCreationConsumer>>();
        _sut = new JobCreationConsumer(
            _consumerMock.Object,
            _repositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStarted_CallsConsumeAsyncWithCreateJobMessage()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _consumerMock
            .Setup(x => x.ConsumeAsync(
                It.IsAny<Func<CreateJobMessage, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(100);
        await _sut.StopAsync(CancellationToken.None);

        // Assert
        _consumerMock.Verify(
            x => x.ConsumeAsync(
                It.IsAny<Func<CreateJobMessage, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleMessage_ValidMessage_CreatesJobInRepository()
    {
        // Arrange
        Func<CreateJobMessage, Task>? capturedHandler = null;
        var jobId = Guid.NewGuid();

        _consumerMock
            .Setup(x => x.ConsumeAsync(
                It.IsAny<Func<CreateJobMessage, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Func<CreateJobMessage, Task>, CancellationToken>((handler, _) =>
                capturedHandler = handler)
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();

        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        capturedHandler.Should().NotBeNull();

        var message = new CreateJobMessage(jobId, "EnviarEmail", "{\"data\": \"test\"}", 3);

        // Act
        await capturedHandler!(message);

        // Assert
        _repositoryMock.Verify(
            x => x.AddAsync(
                It.Is<Job>(j => j.Id == jobId && j.Type == "EnviarEmail"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        await _sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMessage_InvalidMessage_LogsWarningAndDoesNotThrow()
    {
        // Arrange
        Func<CreateJobMessage, Task>? capturedHandler = null;

        _consumerMock
            .Setup(x => x.ConsumeAsync(
                It.IsAny<Func<CreateJobMessage, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Func<CreateJobMessage, Task>, CancellationToken>((handler, _) =>
                capturedHandler = handler)
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();

        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        capturedHandler.Should().NotBeNull();

        var invalidMessage = new CreateJobMessage(Guid.NewGuid(), "", "{\"data\": \"test\"}", 3);

        // Act
        var act = () => capturedHandler!(invalidMessage);

        // Assert
        await act.Should().NotThrowAsync();

        _repositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await _sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMessage_RepositoryThrowsException_PropagatesException()
    {
        // Arrange
        Func<CreateJobMessage, Task>? capturedHandler = null;

        _consumerMock
            .Setup(x => x.ConsumeAsync(
                It.IsAny<Func<CreateJobMessage, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Func<CreateJobMessage, Task>, CancellationToken>((handler, _) =>
                capturedHandler = handler)
            .Returns(Task.CompletedTask);

        _repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MongoDB indisponivel"));

        using var cts = new CancellationTokenSource();

        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        capturedHandler.Should().NotBeNull();

        var message = new CreateJobMessage(Guid.NewGuid(), "EnviarEmail", "{\"data\": \"test\"}", 3);

        // Act
        var act = () => capturedHandler!(message);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("MongoDB indisponivel");

        await _sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMessage_EmptyGuid_LogsWarningAndDoesNotPersist()
    {
        // Arrange
        Func<CreateJobMessage, Task>? capturedHandler = null;

        _consumerMock
            .Setup(x => x.ConsumeAsync(
                It.IsAny<Func<CreateJobMessage, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Func<CreateJobMessage, Task>, CancellationToken>((handler, _) =>
                capturedHandler = handler)
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();

        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        capturedHandler.Should().NotBeNull();

        var message = new CreateJobMessage(Guid.Empty, "EnviarEmail", "{\"data\": \"test\"}", 3);

        // Act
        var act = () => capturedHandler!(message);

        // Assert
        await act.Should().NotThrowAsync();

        _repositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await _sut.StopAsync(CancellationToken.None);
    }
}
