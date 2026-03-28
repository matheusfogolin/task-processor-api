using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskProcessor.Application.Shared.Messages;
using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Domain.Ports.MessageQueue;

namespace TaskProcessor.Worker.Consumers;

public sealed class JobCreationConsumer(
    IMessageQueueConsumer messageQueueConsumer,
    IJobRepository jobRepository,
    ILogger<JobCreationConsumer> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("JobCreationConsumer iniciado. Aguardando mensagens do RabbitMQ.");

        await messageQueueConsumer.ConsumeAsync<CreateJobMessage>(
            message => HandleMessageAsync(message, stoppingToken),
            stoppingToken);

        logger.LogInformation("JobCreationConsumer finalizado.");
    }

    private async Task HandleMessageAsync(CreateJobMessage message, CancellationToken ct)
    {
        var result = Job.Create(message.Id, message.Type, message.Payload, message.MaxRetries);

        if (result.IsError)
        {
            logger.LogWarning(
                "Mensagem inválida recebida. JobId={JobId} Erros={Erros}",
                message.Id,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        try
        {
            await jobRepository.AddAsync(result.Value, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Falha ao persistir job. JobId={JobId}",
                result.Value.Id);
            throw;
        }

        logger.LogInformation(
            "Job criado com sucesso. JobId={JobId} Type={Type}",
            result.Value.Id,
            result.Value.Type);
    }
}
