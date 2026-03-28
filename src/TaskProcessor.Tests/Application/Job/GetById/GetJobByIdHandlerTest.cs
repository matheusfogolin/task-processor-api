using FluentAssertions;
using Moq;
using TaskProcessor.Application.Job.GetById;
using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Domain.Shared.Errors;
using TaskProcessor.Tests.Factories;

namespace TaskProcessor.Tests.Application.Job.GetById;

public class GetJobByIdHandlerTest
{
    private readonly Mock<IJobRepository> _jobRepositoryMock;
    private readonly GetJobByIdHandler _handler;

    public GetJobByIdHandlerTest()
    {
        _jobRepositoryMock = new Mock<IJobRepository>();
        _handler = new GetJobByIdHandler(_jobRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingJob_ShouldReturnJobDto()
    {
        // Arrange
        var job = JobFactory.Valid().Value;
        var query = new GetJobByIdQuery(job.Id);
        var ct = CancellationToken.None;

        _jobRepositoryMock
            .Setup(x => x.GetByIdAsync(job.Id, ct))
            .ReturnsAsync(job);

        // Act
        var result = await _handler.Handle(query, ct);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(job.Id);
        result.Value.Type.Should().Be(job.Type);
        result.Value.Payload.Should().Be(job.Payload);
        result.Value.Status.Should().Be(job.Status);
        result.Value.RetryCount.Should().Be(job.RetryCount);
        result.Value.MaxRetries.Should().Be(job.MaxRetries);
        result.Value.CreatedAt.Should().Be(job.CreatedAt);
        result.Value.UpdatedAt.Should().Be(job.UpdatedAt);
        result.Value.CompletedAt.Should().Be(job.CompletedAt);
        result.Value.ErrorMessage.Should().Be(job.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithNonExistingJob_ShouldReturnNotFoundError()
    {
        // Arrange
        var query = new GetJobByIdQuery(Guid.NewGuid());
        var ct = CancellationToken.None;

        _jobRepositoryMock
            .Setup(x => x.GetByIdAsync(query.Id, ct))
            .Returns(Task.FromResult<TaskProcessor.Domain.Aggregates.JobAggregate.Job?>(null));

        // Act
        var result = await _handler.Handle(query, ct);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.NotFound);
    }
}
