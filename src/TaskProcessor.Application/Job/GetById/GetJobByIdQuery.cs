using MediatR;
using TaskProcessor.Application.Job.GetById.Dtos.Response;
using TaskProcessor.Domain.Shared;

namespace TaskProcessor.Application.Job.GetById;

public record GetJobByIdQuery(Guid Id) : IRequest<Result<JobDto>>;
