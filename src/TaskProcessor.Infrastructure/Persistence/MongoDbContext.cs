using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using TaskProcessor.Domain.Aggregates.JobAggregate;

namespace TaskProcessor.Infrastructure.Persistence;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    static MongoDbContext()
    {
        BsonSerializer.RegisterSerializer(
            new GuidSerializer(GuidRepresentation.Standard));

        var conventionPack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String)
        };

        ConventionRegistry.Register(
            "TaskProcessorConventions",
            conventionPack,
            t => t.Namespace?.StartsWith("TaskProcessor.Domain") == true);

        if (!BsonClassMap.IsClassMapRegistered(typeof(Job)))
        {
            BsonClassMap.RegisterClassMap<Job>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
        }
    }

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<Job> Jobs => _database.GetCollection<Job>("jobs");

    public async Task EnsureIndexesCreatedAsync(CancellationToken ct = default)
    {
        var statusLockedNextRetryIndex = new CreateIndexModel<Job>(
            Builders<Job>.IndexKeys
                .Ascending(j => j.Status)
                .Ascending(j => j.LockedUntil)
                .Ascending(j => j.NextRetryAt),
            new CreateIndexOptions { Name = "idx_status_locked_nextretry" });

        await Jobs.Indexes.CreateOneAsync(statusLockedNextRetryIndex, cancellationToken: ct);
    }
}
