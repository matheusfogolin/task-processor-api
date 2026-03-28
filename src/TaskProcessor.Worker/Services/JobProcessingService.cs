using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskProcessor.Domain.Aggregates.JobAggregate;

namespace TaskProcessor.Worker.Services;

public sealed class JobProcessingService(
    IJobRepository jobRepository,
    ILogger<JobProcessingService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(1);
    private static readonly string WorkerId = $"worker-{Environment.MachineName}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "JobProcessingService iniciado. WorkerId={WorkerId} PollingInterval={PollingInterval}s LeaseDuration={LeaseDuration}s",
            WorkerId,
            PollingInterval.TotalSeconds,
            LeaseDuration.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await jobRepository.AcquireNextPendingJobAsync(
                    WorkerId, LeaseDuration, stoppingToken);

                if (job is null)
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                    continue;
                }

                await ProcessJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Erro inesperado no loop de processamento. Aguardando {PollingInterval}s antes de tentar novamente.",
                    PollingInterval.TotalSeconds);

                try
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        logger.LogInformation("JobProcessingService finalizado. WorkerId={WorkerId}", WorkerId);
    }

    private async Task ProcessJobAsync(Job job, CancellationToken ct)
    {
        logger.LogInformation(
            "Processando job. JobId={JobId} Type={Type}",
            job.Id,
            job.Type);

        try
        {
            await Task.Delay(ProcessingDelay, ct);

            var completeResult = job.MarkAsCompleted();
            if (completeResult.IsError)
            {
                logger.LogWarning(
                    "Falha ao marcar job como concluído. JobId={JobId} Erro={Erro}",
                    job.Id,
                    completeResult.FirstError.Description);
                return;
            }

            await jobRepository.UpdateAsync(job, ct);

            logger.LogInformation(
                "Job concluído com sucesso. JobId={JobId}",
                job.Id);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Falha ao processar job. JobId={JobId} Type={Type}",
                job.Id,
                job.Type);

            var failResult = job.MarkAsFailed(ex.Message);
            if (failResult.IsError)
            {
                logger.LogWarning(
                    "Falha ao marcar job como falho. JobId={JobId} Erro={Erro}",
                    job.Id,
                    failResult.FirstError.Description);
                return;
            }

            await jobRepository.UpdateAsync(job, ct);
        }
    }
}
