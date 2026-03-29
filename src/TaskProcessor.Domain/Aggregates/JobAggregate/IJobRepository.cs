namespace TaskProcessor.Domain.Aggregates.JobAggregate;

public interface IJobRepository
{
    Task AddAsync(Job job, CancellationToken ct = default);
    Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(Job job, CancellationToken ct = default);
    Task<IReadOnlyList<Job>> AcquireNextPendingJobsBatchAsync(
        string workerId,
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken ct = default);
}
