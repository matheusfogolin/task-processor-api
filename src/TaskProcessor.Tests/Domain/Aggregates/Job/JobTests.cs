using FluentAssertions;
using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Domain.Shared.Errors;
using TaskProcessor.Tests.Factories;

namespace TaskProcessor.Tests.Domain.Aggregates.Job;

public class JobTests
{
    [Fact]
    public void Create_WithValidData_ShouldReturnJob()
    {
        var result = JobFactory.Valid();

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.Type.Should().Be("EnviarEmail");
        result.Value.Status.Should().Be(EJobStatus.Pending);
        result.Value.RetryCount.Should().Be(0);
        result.Value.MaxRetries.Should().Be(3);
        result.Value.CompletedAt.Should().BeNull();
        result.Value.ErrorMessage.Should().BeNull();
        result.Value.NextRetryAt.Should().BeNull();
        result.Value.LockedUntil.Should().BeNull();
        result.Value.LockedBy.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyPayload_ShouldReturnError()
    {
        var result = global::TaskProcessor.Domain.Aggregates.JobAggregate.Job.Create(
            Guid.CreateVersion7(), "EnviarEmail", "", 3);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.PayloadRequired);
    }

    [Fact]
    public void Create_WithZeroMaxRetries_ShouldReturnError()
    {
        var result = JobFactory.WithMaxRetries(0);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.InvalidMaxRetries);
    }

    [Fact]
    public void Create_WithEmptyType_ShouldReturnError()
    {
        var result = global::TaskProcessor.Domain.Aggregates.JobAggregate.Job.Create(
            Guid.CreateVersion7(), "", "{\"data\": \"test\"}", 3);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.TypeRequired);
    }

    [Fact]
    public void MarkAsProcessing_FromPending_ShouldSucceed()
    {
        var job = JobFactory.Valid().Value;

        var result = job.MarkAsProcessing();

        result.IsError.Should().BeFalse();
        job.Status.Should().Be(EJobStatus.Processing);
    }

    [Fact]
    public void MarkAsProcessing_FromFailed_ShouldSucceed()
    {
        var job = JobFactory.InFailed();

        var result = job.MarkAsProcessing();

        result.IsError.Should().BeFalse();
        job.Status.Should().Be(EJobStatus.Processing);
    }

    [Fact]
    public void MarkAsProcessing_FromCompleted_ShouldReturnError()
    {
        var job = JobFactory.InCompleted();

        var result = job.MarkAsProcessing();

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.InvalidStatusTransition);
    }

    [Fact]
    public void MarkAsProcessing_FromProcessing_ShouldReturnError()
    {
        var job = JobFactory.InProcessing();

        var result = job.MarkAsProcessing();

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.InvalidStatusTransition);
    }

    [Fact]
    public void MarkAsCompleted_FromProcessing_ShouldSucceed()
    {
        var job = JobFactory.InProcessing();

        var result = job.MarkAsCompleted();

        result.IsError.Should().BeFalse();
        job.Status.Should().Be(EJobStatus.Completed);
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsCompleted_FromPending_ShouldReturnError()
    {
        var job = JobFactory.Valid().Value;

        var result = job.MarkAsCompleted();

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.InvalidStatusTransition);
    }

    [Fact]
    public void MarkAsCompleted_FromCompleted_ShouldReturnError()
    {
        var job = JobFactory.InCompleted();

        var result = job.MarkAsCompleted();

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.InvalidStatusTransition);
    }

    [Fact]
    public void MarkAsFailed_FromProcessing_ShouldSucceed()
    {
        var job = JobFactory.InProcessing();

        var result = job.MarkAsFailed("Falha ao enviar e-mail");

        result.IsError.Should().BeFalse();
        job.Status.Should().Be(EJobStatus.Failed);
        job.ErrorMessage.Should().Be("Falha ao enviar e-mail");
        job.RetryCount.Should().Be(1);
        job.NextRetryAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsFailed_WithEmptyMessage_ShouldReturnError()
    {
        var job = JobFactory.InProcessing();

        var result = job.MarkAsFailed("");

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.ErrorMessageRequired);
    }

    [Fact]
    public void MarkAsFailed_FromPending_ShouldReturnError()
    {
        var job = JobFactory.Valid().Value;

        var result = job.MarkAsFailed("Erro qualquer");

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.InvalidStatusTransition);
    }

    [Fact]
    public void MarkAsFailed_ShouldIncrementRetryCount()
    {
        var job = JobFactory.InProcessing();

        job.MarkAsFailed("Primeira falha");
        job.MarkAsProcessing();
        job.MarkAsFailed("Segunda falha");

        job.RetryCount.Should().Be(2);
    }

    [Fact]
    public void MarkAsFailed_ShouldCalculateNextRetryAtWithExponentialBackoff()
    {
        var job = JobFactory.InProcessing();
        var beforeFail = DateTime.UtcNow;

        job.MarkAsFailed("Falha");

        job.NextRetryAt.Should().NotBeNull();
        job.NextRetryAt.Should().BeAfter(beforeFail);
    }

    [Fact]
    public void IsEligibleForRetry_WhenFailedButNextRetryAtInFuture_ShouldReturnFalse()
    {
        var job = JobFactory.InFailed();

        var eligible = job.IsEligibleForRetry();

        eligible.Should().BeFalse();
    }

    [Fact]
    public void IsEligibleForRetry_WhenMaxRetriesReached_ShouldReturnFalse()
    {
        var job = JobFactory.WithMaxRetries(1).Value;
        job.MarkAsProcessing();
        job.MarkAsFailed("Falha");

        var eligible = job.IsEligibleForRetry();

        eligible.Should().BeFalse();
    }

    [Fact]
    public void IsEligibleForRetry_WhenPending_ShouldReturnFalse()
    {
        var job = JobFactory.Valid().Value;

        var eligible = job.IsEligibleForRetry();

        eligible.Should().BeFalse();
    }

    [Fact]
    public void IsEligibleForRetry_WhenCompleted_ShouldReturnFalse()
    {
        var job = JobFactory.InCompleted();

        var eligible = job.IsEligibleForRetry();

        eligible.Should().BeFalse();
    }

    [Fact]
    public void IsEligibleForRetry_WhenFailedAndNextRetryAtInPast_ShouldReturnTrue()
    {
        var job = JobFactory.InFailed();

        var nextRetryAtField = typeof(global::TaskProcessor.Domain.Aggregates.JobAggregate.Job)
            .GetProperty(nameof(global::TaskProcessor.Domain.Aggregates.JobAggregate.Job.NextRetryAt));
        nextRetryAtField!.SetValue(job, DateTime.UtcNow.AddSeconds(-10));

        var eligible = job.IsEligibleForRetry();

        eligible.Should().BeTrue();
    }

    [Fact]
    public void Create_WithWhitespaceOnlyType_ShouldReturnError()
    {
        var result = global::TaskProcessor.Domain.Aggregates.JobAggregate.Job.Create(
            Guid.CreateVersion7(), "   ", "{\"data\": \"test\"}", 3);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.TypeRequired);
    }

    [Fact]
    public void Create_WithWhitespaceOnlyPayload_ShouldReturnError()
    {
        var result = global::TaskProcessor.Domain.Aggregates.JobAggregate.Job.Create(
            Guid.CreateVersion7(), "EnviarEmail", "   ", 3);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.PayloadRequired);
    }

    [Fact]
    public void MarkAsCompleted_ShouldClearLeaseFields()
    {
        var job = JobFactory.InProcessing();

        job.MarkAsCompleted();

        job.LockedBy.Should().BeNull();
        job.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void MarkAsFailed_ShouldClearLeaseFields()
    {
        var job = JobFactory.InProcessing();

        job.MarkAsFailed("Falha temporária");

        job.LockedBy.Should().BeNull();
        job.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void Create_WithValidIdAndParameters_ShouldReturnJobWithPreservedId()
    {
        var id = Guid.NewGuid();

        var result = JobFactory.ValidWithId(id);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(id);
        result.Value.Type.Should().Be("EnviarEmail");
        result.Value.Status.Should().Be(EJobStatus.Pending);
    }

    [Fact]
    public void Create_WithEmptyGuid_ShouldReturnIdRequiredError()
    {
        var result = global::TaskProcessor.Domain.Aggregates.JobAggregate.Job.Create(
            Guid.Empty, "EnviarEmail", "{\"data\": \"test\"}", 3);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.IdRequired);
    }

    [Fact]
    public void Create_WithId_InvalidType_ShouldReturnTypeRequiredError()
    {
        var result = global::TaskProcessor.Domain.Aggregates.JobAggregate.Job.Create(
            Guid.NewGuid(), "", "{\"data\": \"test\"}", 3);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(JobErrors.TypeRequired);
    }
}
