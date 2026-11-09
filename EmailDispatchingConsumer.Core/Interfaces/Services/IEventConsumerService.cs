namespace EmailDispatchingConsumer.Core.Interfaces.Services;

public interface IEventConsumerService
{
    /// <summary>
    /// Inicia el consumo reactivo y asíncrono de mensajes desde la cola de RabbitMQ.
    /// Ejecuta el delegado messageHandler para cada mensaje entrante y gestiona
    /// automáticamente el ACK si finaliza con éxito o NACK si falla.
    /// </summary>
    Task StartConsumingAsync<T>(
        Func<T, CancellationToken, Task<bool>> messageHandler,
        CancellationToken cancellationToken = default) where T : class;
}