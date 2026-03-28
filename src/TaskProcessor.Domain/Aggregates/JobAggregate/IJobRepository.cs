namespace TaskProcessor.Domain.Aggregates.JobAggregate;

public interface IJobRepository
{
    Task AddAsync(Job job, CancellationToken ct = default);
    Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(Job job, CancellationToken ct = default);
    Task<Job?> AcquireNextPendingJobAsync(string workerId, TimeSpan leaseDuration, CancellationToken ct = default);
}
