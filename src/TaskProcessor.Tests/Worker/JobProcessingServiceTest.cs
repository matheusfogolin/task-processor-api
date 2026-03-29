using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Tests.Factories;
using TaskProcessor.Worker.Services;
using TaskProcessor.Worker.Settings;

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

        var settings = Options.Create(new JobProcessingSettings { MaxParallelJobs = 10 });

        _sut = new JobProcessingService(
            _repositoryMock.Object,
            settings,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_PendingJobExists_AcquiresAndCompletesJob()
    {
        // Arrange
        var job = JobFactory.InProcessing();
        using var cts = new CancellationTokenSource();

        _repositoryMock
            .SetupSequence(x => x.AcquireNextPendingJobsBatchAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job> { job })
            .ReturnsAsync(new List<Job>());

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
            .SetupSequence(x => x.AcquireNextPendingJobsBatchAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job> { job })
            .ReturnsAsync(new List<Job>());

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
            .Setup(x => x.AcquireNextPendingJobsBatchAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job>());

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
            .Setup(x => x.AcquireNextPendingJobsBatchAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job>());

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
            .SetupSequence(x => x.AcquireNextPendingJobsBatchAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MongoDB indisponivel"))
            .ReturnsAsync(new List<Job>());

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(6000);
        await cts.CancelAsync();

        var act = () => _sut.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        _repositoryMock.Verify(
            x => x.AcquireNextPendingJobsBatchAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task ExecuteAsync_BatchWithMultipleJobs_ProcessesAllJobsInBatch()
    {
        // Arrange
        var job1 = JobFactory.InProcessing();
        var job2 = JobFactory.InProcessing();
        using var cts = new CancellationTokenSource();

        _repositoryMock
            .SetupSequence(x => x.AcquireNextPendingJobsBatchAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job> { job1, job2 })
            .ReturnsAsync(new List<Job>());

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(3000);
        await cts.CancelAsync();
        await _sut.StopAsync(CancellationToken.None);

        // Assert
        job1.Status.Should().Be(EJobStatus.Completed);
        job2.Status.Should().Be(EJobStatus.Completed);

        _repositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_OneJobInBatchFails_OtherJobStillCompletes()
    {
        // Arrange
        var jobOk = JobFactory.InProcessing();
        var jobFail = JobFactory.InProcessing();
        using var cts = new CancellationTokenSource();

        _repositoryMock
            .SetupSequence(x => x.AcquireNextPendingJobsBatchAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job> { jobOk, jobFail })
            .ReturnsAsync(new List<Job>());

        _repositoryMock
            .Setup(x => x.UpdateAsync(
                It.Is<Job>(j => j.Id == jobFail.Id),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Falha simulada no UpdateAsync"));

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(3000);
        await cts.CancelAsync();
        await _sut.StopAsync(CancellationToken.None);

        // Assert
        jobOk.Status.Should().Be(EJobStatus.Completed);


        jobFail.Status.Should().Be(EJobStatus.Completed);

        _repositoryMock.Verify(
            x => x.UpdateAsync(
                It.Is<Job>(j => j.Id == jobOk.Id && j.Status == EJobStatus.Completed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_MaxParallelJobsPassedToBatchMethod()
    {
        // Arrange
        var expectedBatchSize = 10;
        using var cts = new CancellationTokenSource();

        _repositoryMock
            .Setup(x => x.AcquireNextPendingJobsBatchAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job>());

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await _sut.StopAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(
            x => x.AcquireNextPendingJobsBatchAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                expectedBatchSize,
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
