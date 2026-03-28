using MongoDB.Bson;
using MongoDB.Driver;
using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Infrastructure.Persistence;

namespace TaskProcessor.Infrastructure.Repositories;

public sealed class JobRepository(MongoDbContext context) : IJobRepository
{
    public async Task AddAsync(Job job, CancellationToken ct = default)
    {
        await context.Jobs.InsertOneAsync(job, cancellationToken: ct);
    }

    public async Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Jobs
            .Find(j => j.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateAsync(Job job, CancellationToken ct = default)
    {
        var result = await context.Jobs.ReplaceOneAsync(
            j => j.Id == job.Id,
            job,
            cancellationToken: ct);

        if (result.MatchedCount == 0)
            throw new InvalidOperationException(
                $"Job {job.Id} não encontrado para atualização.");
    }

    public async Task<Job?> AcquireNextPendingJobAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var pendingFilter = Builders<Job>.Filter.Eq(j => j.Status, EJobStatus.Pending);

        var retryEligibleFilter = BuildRetryEligibleFilter(now);

        var statusFilter = Builders<Job>.Filter.Or(pendingFilter, retryEligibleFilter);

        var leaseFilter = Builders<Job>.Filter.Or(
            Builders<Job>.Filter.Eq(j => j.LockedUntil, (DateTime?)null),
            Builders<Job>.Filter.Lte(j => j.LockedUntil, now));

        var filter = Builders<Job>.Filter.And(statusFilter, leaseFilter);

        var update = Builders<Job>.Update
            .Set(j => j.Status, EJobStatus.Processing)
            .Set(j => j.LockedBy, workerId)
            .Set(j => j.LockedUntil, now.Add(leaseDuration))
            .Set(j => j.UpdatedAt, now);

        var options = new FindOneAndUpdateOptions<Job>
        {
            ReturnDocument = ReturnDocument.After,
            Sort = Builders<Job>.Sort.Ascending(j => j.CreatedAt)
        };

        return await context.Jobs.FindOneAndUpdateAsync(filter, update, options, ct);
    }

    private static FilterDefinition<Job> BuildRetryEligibleFilter(DateTime now)
    {
        return Builders<Job>.Filter.And(
            Builders<Job>.Filter.Eq(j => j.Status, EJobStatus.Failed),
            new BsonDocument("$expr",
                new BsonDocument("$lt", new BsonArray { "$retryCount", "$maxRetries" })),
            Builders<Job>.Filter.Lte(j => j.NextRetryAt, now));
    }
}
