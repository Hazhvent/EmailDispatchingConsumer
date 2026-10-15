using EmailDispatchingConsumer.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace EmailDispatchingConsumer.Infrastructure.Services;

public sealed class LoggingService<T>(ILogger<T> logger) : ILoggingService<T>
{
    public void LogInfo(string message, params object[] args)
    {
        logger.LogInformation(message, args);
    }

    public void LogWarning(string message, params object[] args)
    {
        logger.LogWarning(message, args);
    }

    public void LogError(Exception? exception, string message, params object[] args)
    {
        logger.LogError(exception, message, args);
    }

    public void LogCritical(Exception? exception, string message, params object[] args)
    {
        logger.LogCritical(exception, message, args);
    }
}