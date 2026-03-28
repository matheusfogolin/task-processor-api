namespace TaskProcessor.Domain.Ports.MessageQueue;

public interface IMessageQueuePublisher
{
    Task PublishAsync<T>(T message, CancellationToken ct = default);
}
