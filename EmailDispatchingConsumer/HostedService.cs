namespace EmailDispatchingConsumer;

using EmailDispatchingConsumer.Application.UseCases;
using EmailDispatchingConsumer.Core.Constants.Messages;
using EmailDispatchingConsumer.Core.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class HostedService(
    IServiceScopeFactory serviceScopeFactory,
    IEventConsumerService eventConsumerService,
    ILoggingService<HostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInfo(LogsConstants.INFO_WORKER_STARTING);
        logger.LogInfo(LogsConstants.INFO_AUTHOR);
        logger.LogInfo(LogsConstants.INFO_CONTACT);

        try
        {
            // Precarga inicial de rutas
            using (IServiceScope warmupScope = serviceScopeFactory.CreateScope())
            {
                ISender warmupSender = warmupScope.ServiceProvider.GetRequiredService<ISender>();
                await warmupSender.Send(new WarmupConsumerRoutesCommand(), stoppingToken);
            }

            // Suscripción reactiva con propagación de estado a RabbitMQ
            await eventConsumerService.StartConsumingAsync<DocumentProcessedEvent>(
                async (documentEvent, ct) =>
                {
                    using IServiceScope scope = serviceScopeFactory.CreateScope();
                    ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

                    // 🟢 Ahora retorna el bool real del comando:
                    // true  -> BasicAckAsync (procesado o archivado en error correctamente)
                    // false -> BasicNackAsync (falla técnica de red/BD -> DLQ o reintento)
                    return await sender.Send(new ProcessEmailDispatchCommand(documentEvent), ct);
                },
                stoppingToken
            );

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInfo(LogsConstants.INFO_WORKER_STOP_SIGNAL);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, LogsConstants.ERROR_WORKER_CRITICAL, ex.Message);
            throw;
        }
        finally
        {
            logger.LogInfo(LogsConstants.INFO_WORKER_STOPPED);
        }
    }
}