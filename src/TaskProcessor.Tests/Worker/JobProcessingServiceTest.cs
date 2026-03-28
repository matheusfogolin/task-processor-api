using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Tests.Factories;
using TaskProcessor.Worker.Services;

namespace TaskProcessor.Tests.Worker;

public class JobProcessingServiceTest
{
    private readonly Mock<IJobRepository> _repositoryMock;
    private readonly Mock<ILogger<JobProcessingService>> _loggerMock;
    private readonly JobProcessingService _sut;

    public JobProcessingServiceTest()
    {
        _repositoryMock = new Mock<IJobRepository>();
        _loggerMock = new Mock<ILogger<JobProcessingService>>();
        _sut = new JobProcessingService(
            _repositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_PendingJobExists_AcquiresAndCompletesJob()
    {
        // Arrange
        var job = JobFactory.InProcessing();
        using var cts = new CancellationTokenSource();

        _repositoryMock
            .SetupSequence(x => x.AcquireNextPendingJobAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(job)
            .ReturnsAsync((Job?)null);

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(3000);
        await cts.CancelAsync();
        await _sut.StopAsync(CancellationToken.None);

        // Assert
        job.Status.Should().Be(EJobStatus.Completed);

        _repositoryMock.Verify(
            x => x.UpdateAsync(
                It.Is<Job>(j => j.Id == job.Id && j.Status == EJobStatus.Completed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UpdateAsyncThrowsAfterCompletion_LogsErrorAndContinues()
    {
        // Arrange
        var job = JobFactory.InProcessing();
        using var cts = new CancellationTokenSource();

        _repositoryMock
            .SetupSequence(x => x.AcquireNextPendingJobAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(job)
            .ReturnsAsync((Job?)null);

        _repositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Falha simulada no UpdateAsync"));

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(3500);
        await cts.CancelAsync();

        var act = () => _sut.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        _repositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_NoJobsAvailable_WaitsWithoutError()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _repositoryMock
            .Setup(x => x.AcquireNextPendingJobAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();

        var act = () => _sut.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_StopsGracefully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _repositoryMock
            .Setup(x => x.AcquireNextPendingJobAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(200);
        await cts.CancelAsync();

        var act = () => _sut.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        _repositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_AcquireThrowsException_LogsErrorAndContinuesPolling()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _repositoryMock
            .SetupSequence(x => x.AcquireNextPendingJobAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MongoDB indisponivel"))
            .ReturnsAsync((Job?)null);

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(6000);
        await cts.CancelAsync();

        var act = () => _sut.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        _repositoryMock.Verify(
            x => x.AcquireNextPendingJobAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task ExecuteAsync_MultipleJobsAvailable_ProcessesSequentially()
    {
        // Arrange
        var job1 = JobFactory.InProcessing();
        var job2 = JobFactory.InProcessing();
        using var cts = new CancellationTokenSource();

        _repositoryMock
            .SetupSequence(x => x.AcquireNextPendingJobAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(job1)
            .ReturnsAsync(job2)
            .ReturnsAsync((Job?)null);

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(5000);
        await cts.CancelAsync();
        await _sut.StopAsync(CancellationToken.None);

        // Assert
        job1.Status.Should().Be(EJobStatus.Completed);
        job2.Status.Should().Be(EJobStatus.Completed);

        _repositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
