namespace TaskProcessor.Domain.Aggregates.JobAggregate;

public enum EJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
