namespace TaskProcessor.Infrastructure.MessageQueue;

public sealed class RabbitMqSettings
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "jobs";
    public int MaxRetries { get; set; } = 3;
    public int RetryTtlMs { get; set; } = 30000;
    public int PublishMaxRetries { get; set; } = 3;
    public int PublishRetryBaseDelayMs { get; set; } = 1000;
}
