using EmailDispatchingConsumer.Core.Constants.Consumer;
using EmailDispatchingConsumer.Core.Constants.Messages;
using EmailDispatchingConsumer.Core.Interfaces.Repositories;
using EmailDispatchingConsumer.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace EmailDispatchingConsumer.Infrastructure.Persistence;

public static class DatabaseConfiguration
{
    public static TBuilder AddDataBase<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        string? connectionString = builder.Configuration.GetConnectionString(SettingsConstants.DB_CONNECTION_STRING_NAME);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                string.Format(LogsConstants.ERROR_NOT_FOUND_CONFIG_SECTION, SettingsConstants.DB_CONNECTION_STRING_NAME));
        }

        builder.Services.AddScoped<IDbConnection>(sp =>
        {
            return new OracleConnection(connectionString);
        });

        AddRepositories(builder.Services);

        return builder;
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<IRepository, Repository>();
    }
}