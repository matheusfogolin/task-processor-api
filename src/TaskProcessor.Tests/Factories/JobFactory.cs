using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Domain.Shared;

namespace TaskProcessor.Tests.Factories;

public static class JobFactory
{
    public static Result<Job> Valid()
    {
        return Job.Create(
            Guid.CreateVersion7(),
            "EnviarEmail",
            "{\"to\": \"user@example.com\", \"subject\": \"Teste\"}",
            3);
    }

    public static Result<Job> ValidWithId(Guid id)
    {
        return Job.Create(
            id,
            "EnviarEmail",
            "{\"to\": \"user@example.com\", \"subject\": \"Teste\"}",
            3);
    }

    public static Result<Job> WithType(string type)
    {
        return Job.Create(Guid.CreateVersion7(), type, "{\"data\": \"test\"}", 3);
    }

    public static Result<Job> WithMaxRetries(int maxRetries)
    {
        return Job.Create(Guid.CreateVersion7(), "EnviarEmail", "{\"data\": \"test\"}", maxRetries);
    }

    public static Job InProcessing()
    {
        var job = Valid().Value;
        job.MarkAsProcessing();
        return job;
    }

    public static Job InFailed(string errorMessage = "Falha temporária")
    {
        var job = InProcessing();
        job.MarkAsFailed(errorMessage);
        return job;
    }

    public static Job InCompleted()
    {
        var job = InProcessing();
        job.MarkAsCompleted();
        return job;
    }
}
