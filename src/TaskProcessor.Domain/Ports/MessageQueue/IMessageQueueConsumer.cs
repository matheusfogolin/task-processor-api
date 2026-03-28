namespace TaskProcessor.Domain.Ports.MessageQueue;

public interface IMessageQueueConsumer
{
    Task ConsumeAsync<T>(Func<T, Task> handler, CancellationToken ct = default);
}
