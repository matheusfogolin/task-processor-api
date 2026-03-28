using TaskProcessor.Domain.Aggregates.JobAggregate;

namespace TaskProcessor.Application.Job.GetById.Dtos.Response;

public record JobDto(
    Guid Id,
    string Type,
    string Payload,
    EJobStatus Status,
    int RetryCount,
    int MaxRetries,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage)
{
    public JobDto(Domain.Aggregates.JobAggregate.Job job)
        : this(
            job.Id,
            job.Type,
            job.Payload,
            job.Status,
            job.RetryCount,
            job.MaxRetries,
            job.CreatedAt,
            job.UpdatedAt,
            job.CompletedAt,
            job.ErrorMessage)
    {
    }
}
