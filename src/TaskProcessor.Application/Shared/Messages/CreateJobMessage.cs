namespace TaskProcessor.Application.Shared.Messages;

public record CreateJobMessage(
    Guid Id,
    string Type,
    string Payload,
    int MaxRetries);
