using TaskProcessor.Domain.Shared;
using TaskProcessor.Domain.Shared.Errors;

namespace TaskProcessor.Domain.Aggregates.JobAggregate;

public sealed class Job
{
    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Payload { get; private set; }
    public EJobStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public string? LockedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    private Job()
    {
        Type = null!;
        Payload = null!;
    }

    private Job(Guid id, string type, string payload, int maxRetries)
    {
        Id = id;
        Type = type;
        Payload = payload;
        Status = EJobStatus.Pending;
        RetryCount = 0;
        MaxRetries = maxRetries;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Result<Job> Create(Guid id, string type, string payload, int maxRetries)
    {
        if (id == Guid.Empty)
            return JobErrors.IdRequired;

        if (string.IsNullOrWhiteSpace(type))
            return JobErrors.TypeRequired;

        if (string.IsNullOrWhiteSpace(payload))
            return JobErrors.PayloadRequired;

        if (maxRetries < 1)
            return JobErrors.InvalidMaxRetries;

        return new Job(id, type, payload, maxRetries);
    }

    public Result<Success> MarkAsProcessing()
    {
        if (Status != EJobStatus.Pending && Status != EJobStatus.Failed)
            return JobErrors.InvalidStatusTransition;

        Status = EJobStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
        return Result.Ok;
    }

    public Result<Success> MarkAsCompleted()
    {
        if (Status != EJobStatus.Processing)
            return JobErrors.InvalidStatusTransition;

        Status = EJobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        LockedBy = null;
        LockedUntil = null;
        UpdatedAt = DateTime.UtcNow;
        return Result.Ok;
    }

    public Result<Success> MarkAsFailed(string errorMessage)
    {
        if (Status != EJobStatus.Processing)
            return JobErrors.InvalidStatusTransition;

        if (string.IsNullOrWhiteSpace(errorMessage))
            return JobErrors.ErrorMessageRequired;

        Status = EJobStatus.Failed;
        ErrorMessage = errorMessage;
        RetryCount++;
        NextRetryAt = CalculateNextRetryAt(RetryCount);
        LockedBy = null;
        LockedUntil = null;
        UpdatedAt = DateTime.UtcNow;
        return Result.Ok;
    }

    public bool IsEligibleForRetry()
    {
        return Status == EJobStatus.Failed
            && RetryCount < MaxRetries
            && (!NextRetryAt.HasValue || NextRetryAt <= DateTime.UtcNow);
    }

    private static DateTime CalculateNextRetryAt(int retryCount)
    {
        var delaySeconds = Math.Pow(2, retryCount);
        return DateTime.UtcNow.AddSeconds(delaySeconds);
    }
}
