using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TaskProcessor.Application.Job.Create;
using TaskProcessor.Application.Job.Create.Dtos.Request;
using TaskProcessor.Application.Job.Create.Dtos.Response;
using TaskProcessor.Application.Job.GetById;
using TaskProcessor.Application.Job.GetById.Dtos.Response;
using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Domain.Shared;
using TaskProcessor.Presentation.Controllers;

namespace TaskProcessor.Tests.Presentation;

public class JobControllerTest
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly JobController _controller;

    public JobControllerTest()
    {
        _controller = new JobController(_senderMock.Object);
    }

    [Fact]
    public async Task Create_WithValidRequest_ShouldReturn201()
    {
        var responseDto = new CreateJobResponseDto(Guid.NewGuid(), "email");
        Result<CreateJobResponseDto> successResult = responseDto;

        _senderMock
            .Setup(x => x.Send(It.IsAny<CreateJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        var request = new CreateJobRequestDto("email", "{}");
        var actionResult = await _controller.Create(request, CancellationToken.None);

        var createdResult = actionResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task Create_WithValidationError_ShouldReturn400()
    {
        Result<CreateJobResponseDto> errorResult =
            DomainError.Validation("Job.PayloadRequired", "O payload é obrigatório.");

        _senderMock
            .Setup(x => x.Send(It.IsAny<CreateJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(errorResult);

        var request = new CreateJobRequestDto("email", "");
        var actionResult = await _controller.Create(request, CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetById_WithExistingId_ShouldReturn200()
    {
        var id = Guid.NewGuid();
        var jobDto = new JobDto(
            id,
            "email",
            "{}",
            EJobStatus.Pending,
            0,
            3,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        Result<JobDto> successResult = jobDto;

        _senderMock
            .Setup(x => x.Send(It.IsAny<GetJobByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        var actionResult = await _controller.GetById(id, CancellationToken.None);

        actionResult.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WithNonExistingId_ShouldReturn404()
    {
        Result<JobDto> errorResult =
            DomainError.NotFound("Job.NotFound", "Tarefa não encontrada.");

        _senderMock
            .Setup(x => x.Send(It.IsAny<GetJobByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(errorResult);

        var actionResult = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);
    }
}
