namespace EmailDispatchingConsumer.Application.Configuration;

using DigitalSigningTaskRunner.Core.Models;
using EmailDispatchingConsumer.Core.Constants.Consumer;
using EmailDispatchingConsumer.Core.Interfaces.Services;
using EmailDispatchingConsumer.Core.Models;
using EmailDispatchingConsumer.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

public static class ConsumerConfiguration
{
    public static TBuilder AddConsumer<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Habilita codificaciones de páginas de códigos (ANSI / Windows-1252) para PDF
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        AddSettings(builder.Services, builder.Configuration);
        AddServices(builder.Services);

        return builder;
    }

    private static void AddServices(IServiceCollection services)
    {
        // 1. Trazabilidad y Logs Estructurados
        services.AddSingleton(typeof(ILoggingService<>), typeof(LoggingService<>));

        // 2. Infraestructura NAS / Gestión de Archivos en Red
        services.AddSingleton<INetworkConnectionService, NetworkConnectionService>();
        services.AddSingleton<IStorageService, StorageService>();

        // 3. Despacho de Correo y Notificaciones (Cliente HTTP Tipado - Pegasus/Softnet)
        services.AddHttpClient<IEmailDispatchingService, EmailDispatchingService>();

        // 4. Mensajería y Consumo de Eventos (RabbitMQ)
        services.AddSingleton<IEventConsumerService, EventConsumerService>();

        // 5. CQRS / Manejadores de Comandos y Consultas (MediatR)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });
    }

    private static void AddSettings(IServiceCollection services, IConfiguration configuration)
    {
        // Mapeo fuertemente tipado de las secciones de appsettings.json
        services.Configure<ConsumerSettings>(configuration.GetSection(SettingsConstants.CONSUMER_SETTINGS_SECTION_NAME));
        services.Configure<NASSettings>(configuration.GetSection(SettingsConstants.NAS_SETTINGS_SECTION_NAME));
        services.Configure<ApiPegasusSettings>(configuration.GetSection(SettingsConstants.API_PEGASUS_SETTINGS_SECTION_NAME));
        services.Configure<RabbitMQSettings>(configuration.GetSection(SettingsConstants.RABBITMQ_SETTINGS_SECTION_NAME));
    }
}