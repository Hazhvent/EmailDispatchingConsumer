using EmailDispatchingConsumer.Core.Constants.Messages;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.Diagnostics;

namespace EmailDispatchingConsumer.Application.Configuration;

public static class LoggingConfiguration
{
    public static TBuilder AddLogs<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));

        string logFilePath = builder.Configuration[LogsConstants.LOGGER_FILE_PATH_KEY]!;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override(LogsConstants.LOGGER_CATEGORY_MICROSOFT, LogEventLevel.Warning)
            .MinimumLevel.Override(LogsConstants.LOGGER_CATEGORY_MICROSOFT_HOSTING_LIFETIME, LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: LogsConstants.LOGGER_CONSOLE_OUTPUT_TEMPLATE)
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: LogsConstants.LOGGER_FILE_RETAINED_COUNT_LIMIT,
                outputTemplate: LogsConstants.LOGGER_FILE_OUTPUT_TEMPLATE)
            .CreateLogger();

        builder.Services.AddSerilog(Log.Logger);

        return builder;
    }
}