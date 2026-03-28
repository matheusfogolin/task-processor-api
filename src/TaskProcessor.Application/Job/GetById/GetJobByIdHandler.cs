using MediatR;
using TaskProcessor.Application.Job.GetById.Dtos.Response;
using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Domain.Shared;
using TaskProcessor.Domain.Shared.Errors;

namespace TaskProcessor.Application.Job.GetById;

public sealed class GetJobByIdHandler(
    IJobRepository jobRepository)
    : IRequestHandler<GetJobByIdQuery, Result<JobDto>>
{
    public async Task<Result<JobDto>> Handle(
        GetJobByIdQuery request,
        CancellationToken ct)
    {
        var job = await jobRepository.GetByIdAsync(request.Id, ct);

        if (job is null)
            return JobErrors.NotFound;

        return new JobDto(job);
    }
}
