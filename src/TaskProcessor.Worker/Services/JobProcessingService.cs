using Microsoft.Extensions.Options;
using TaskProcessor.Domain.Aggregates.JobAggregate;
using TaskProcessor.Worker.Settings;

namespace TaskProcessor.Worker.Services;

public sealed class JobProcessingService(
    IJobRepository jobRepository,
    IOptions<JobProcessingSettings> settings,
    ILogger<JobProcessingService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(1);
    private static readonly string WorkerId = $"worker-{Environment.MachineName}";

    private readonly int _maxParallelJobs = settings.Value.MaxParallelJobs > 0
        ? settings.Value.MaxParallelJobs
        : throw new ArgumentOutOfRangeException(nameof(settings), "MaxParallelJobs deve ser maior que zero.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "JobProcessingService iniciado. WorkerId={WorkerId} PollingInterval={PollingInterval}s LeaseDuration={LeaseDuration}s MaxParallelJobs={MaxParallelJobs}",
            WorkerId,
            PollingInterval.TotalSeconds,
            LeaseDuration.TotalSeconds,
            _maxParallelJobs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobs = await jobRepository.AcquireNextPendingJobsBatchAsync(
                    WorkerId, LeaseDuration, _maxParallelJobs, stoppingToken);

                if (jobs.Count == 0)
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                    continue;
                }

                await Task.WhenAll(jobs.Select(j => ProcessJobAsync(j, stoppingToken)));
                await Task.Delay(PollingInterval, stoppingToken);
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
                    "Falha ao marcar job como concluido. JobId={JobId} Erro={Erro}",
                    job.Id,
                    completeResult.FirstError.Description);
                return;
            }

            await jobRepository.UpdateAsync(job, ct);

            logger.LogInformation(
                "Job concluido com sucesso. JobId={JobId}",
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

            try
            {
                await jobRepository.UpdateAsync(job, ct);
            }
            catch (Exception updateEx)
            {
                logger.LogError(updateEx,
                    "Falha ao persistir status Failed do job. JobId={JobId}",
                    job.Id);
            }
        }
    }
}
