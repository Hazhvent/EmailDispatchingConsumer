namespace EmailDispatchingConsumer.Infrastructure.Services;

using DigitalSigningTaskRunner.Core.Models;
using EmailDispatchingConsumer.Core.Constants.Messages;
using EmailDispatchingConsumer.Core.Interfaces.Services;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

public sealed class EventConsumerService : IEventConsumerService, IAsyncDisposable
{
    private readonly RabbitMQSettings _rabbitSettings;
    private readonly ILoggingService<EventConsumerService> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public EventConsumerService(
        IOptions<RabbitMQSettings> rabbitOptions,
        ILoggingService<EventConsumerService> logger)
    {
        _rabbitSettings = rabbitOptions.Value;
        _logger = logger;
    }

    private async Task InitializeRabbitMqAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null && _connection is not null) return;

        var factory = new ConnectionFactory
        {
            HostName = _rabbitSettings.Host,
            Port = _rabbitSettings.Port,
            VirtualHost = _rabbitSettings.VirtualHost,
            UserName = _rabbitSettings.Username,
            Password = _rabbitSettings.Password
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // 1. Declarar Exchange
        await _channel.ExchangeDeclareAsync(
            exchange: _rabbitSettings.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken
        );

        // 2. Declarar Cola
        await _channel.QueueDeclareAsync(
            queue: _rabbitSettings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken
        );

        // 3. Bind entre Cola y Exchange
        await _channel.QueueBindAsync(
            queue: _rabbitSettings.QueueName,
            exchange: _rabbitSettings.ExchangeName,
            routingKey: _rabbitSettings.RoutingKey,
            arguments: null,
            cancellationToken: cancellationToken
        );

        // 4. QoS (Prefetch Count = 1): Procesa un mensaje a la vez de forma equitativa
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: cancellationToken
        );
    }

    public async Task StartConsumingAsync<T>(
        Func<T, CancellationToken, Task<bool>> messageHandler,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            await InitializeRabbitMqAsync(cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(_channel!);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                bool processedSuccessfully = false;

                try
                {
                    byte[] body = ea.Body.ToArray();
                    T? message = JsonSerializer.Deserialize<T>(body);

                    if (message is null)
                    {
                        _logger.LogError(null, LogsConstants.ERROR_RABBITMQ_DESERIALIZE, _rabbitSettings.QueueName);
                        // Mensaje corrupto o formato irreconocible: descartar (Nack sin requeue)
                        await _channel!.BasicNackAsync(
                            deliveryTag: ea.DeliveryTag,
                            multiple: false,
                            requeue: false,
                            cancellationToken: cancellationToken
                        );
                        return;
                    }

                    // Delegar el procesamiento al Worker consumidor
                    processedSuccessfully = await messageHandler(message, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, LogsConstants.ERROR_RABBITMQ_CONSUME, ex.Message);
                    processedSuccessfully = false;
                }

                if (processedSuccessfully)
                {
                    // ACK: Notificación enviada y PDF movido a Notificados
                    await _channel!.BasicAckAsync(
                        deliveryTag: ea.DeliveryTag,
                        multiple: false,
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    // NACK: Falló; se rechaza sin requeue para evitar ciclos infinitos (o va a DLQ)
                    await _channel!.BasicNackAsync(
                        deliveryTag: ea.DeliveryTag,
                        multiple: false,
                        requeue: false,
                        cancellationToken: cancellationToken
                    );
                }
            };

            // Iniciar consumo con autoAck en false
            await _channel!.BasicConsumeAsync(
                queue: _rabbitSettings.QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, LogsConstants.ERROR_RABBITMQ_CONSUME, ex.Message);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }
}