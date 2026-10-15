namespace EmailDispatchingConsumer.Core.Interfaces.Services;

public interface ILoggingService<T>
{
    void LogInfo(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(Exception? exception, string message, params object[] args);
    void LogCritical(Exception? exception, string message, params object[] args);
}