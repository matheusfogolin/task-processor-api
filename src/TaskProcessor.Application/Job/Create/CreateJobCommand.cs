using MediatR;
using TaskProcessor.Application.Job.Create.Dtos.Response;
using TaskProcessor.Domain.Shared;

namespace TaskProcessor.Application.Job.Create;

public record CreateJobCommand(string Type, string Payload) : IRequest<Result<CreateJobResponseDto>>;
