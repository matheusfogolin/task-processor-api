using MediatR;
using Microsoft.Extensions.Options;
using TaskProcessor.Application.Job.Create.Dtos.Response;
using TaskProcessor.Application.Shared;
using TaskProcessor.Application.Shared.Messages;
using TaskProcessor.Domain.Ports.MessageQueue;
using TaskProcessor.Domain.Shared;

namespace TaskProcessor.Application.Job.Create;

public sealed class CreateJobHandler(
    IMessageQueuePublisher messageQueuePublisher,
    IOptions<JobSettings> jobSettings)
    : IRequestHandler<CreateJobCommand, Result<CreateJobResponseDto>>
{
    public async Task<Result<CreateJobResponseDto>> Handle(
        CreateJobCommand request,
        CancellationToken ct)
    {
        var maxRetries = jobSettings.Value.MaxRetries ?? 3;
        var id = Guid.CreateVersion7();

        var message = new CreateJobMessage(id, request.Type, request.Payload, maxRetries);
        await messageQueuePublisher.PublishAsync(message, ct);

        return new CreateJobResponseDto(id, request.Type);
    }
}
