namespace EmailDispatchingConsumer.Application.UseCases;

using EmailDispatchingConsumer.Application.Actions;
using EmailDispatchingConsumer.Core.Constants.Consumer;
using EmailDispatchingConsumer.Core.Constants.Messages;
using EmailDispatchingConsumer.Core.Dto;
using EmailDispatchingConsumer.Core.Interfaces.Repositories;
using EmailDispatchingConsumer.Core.Interfaces.Services;
using MediatR;

public sealed record ProcessEmailDispatchCommand(DocumentProcessedEvent DocumentEvent) : IRequest<bool>;

public sealed record WarmupConsumerRoutesCommand : IRequest<bool>;

public class ProcessEmailDispatchHandler(
    IRepository repository,
    INetworkConnectionService networkConnection,
    IStorageService storageService,
    ISender sender,
    ILoggingService<ProcessEmailDispatchHandler> logger)
    : IRequestHandler<ProcessEmailDispatchCommand, bool>,
      IRequestHandler<WarmupConsumerRoutesCommand, bool>
{
    private static readonly SemaphoreSlim _routeLock = new(1, 1);
    private static bool _isFirstRun = true;
    private static string _cachedRutaFirmados = string.Empty;
    private static string _cachedRutaNotificados = string.Empty;
    private static string _cachedRutaErrorNoNotificados = string.Empty;

    public async Task<bool> Handle(WarmupConsumerRoutesCommand request, CancellationToken cancellationToken)
    {
        return await EnsureRoutesLoadedAsync();
    }

    public async Task<bool> Handle(ProcessEmailDispatchCommand command, CancellationToken cancellationToken)
    {
        DocumentProcessedEvent docEvent = command.DocumentEvent;

        // 1. Validar rutas. Si falla la infraestructura (BD o NAS), retornamos FALSE para NACK en RabbitMQ
        if (!await EnsureRoutesLoadedAsync())
        {
            logger.LogError(null, LogsConstants.ERROR_NOT_FOUND_CONFIG_SECTION, LogsConstants.ARG_DB_ROUTES_NAME);
            return false;
        }

        try
        {
            string sourceFilePath = Path.Combine(_cachedRutaFirmados, docEvent.FileName);

            // 2. Leer bytes del PDF
            byte[]? pdfBytes = null;
            await networkConnection.RunInNetworkScope(fileStore =>
            {
                pdfBytes = storageService.ReadNetworkFileBytes(fileStore, sourceFilePath);
            });

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                logger.LogError(null, LogsConstants.ERROR_PDF_BYTES_EMPTY, docEvent.FileName);

                // Si no hay archivo físico en origen, se traslada a error (si existe) y se da por gestionado
                await sender.Send(new ArchiveDocumentActionRequest(
                    SourceFilePath: sourceFilePath,
                    TargetBaseDirectory: _cachedRutaErrorNoNotificados,
                    FileName: docEvent.FileName,
                    FileBytes: null
                ), cancellationToken);

                return true;
            }

            // 3. Ensamblar TramaEnvio
            TramaEnvio? tramaEnvio = await sender.Send(
                new BuildPegasusPayloadActionRequest(docEvent.FileName),
                cancellationToken
            );

            // Si los metadatos fallan o el archivo no pertenece al lote, se archiva en ERROR_NO_NOTIFICADOS
            if (tramaEnvio == null)
            {
                await sender.Send(new ArchiveDocumentActionRequest(
                    SourceFilePath: sourceFilePath,
                    TargetBaseDirectory: _cachedRutaErrorNoNotificados,
                    FileName: docEvent.FileName,
                    FileBytes: pdfBytes
                ), cancellationToken);

                return true;
            }

            // 4. Despachar correo hacia API Pegasus
            bool dispatchSuccess = await sender.Send(
                new ExecuteEmailDispatchActionRequest(tramaEnvio, pdfBytes),
                cancellationToken
            );

            // 5. Trasladar según resultado del despacho
            string targetBasePath = dispatchSuccess ? _cachedRutaNotificados : _cachedRutaErrorNoNotificados;

            bool moved = await sender.Send(new ArchiveDocumentActionRequest(
                SourceFilePath: sourceFilePath,
                TargetBaseDirectory: targetBasePath,
                FileName: docEvent.FileName,
                FileBytes: pdfBytes
            ), cancellationToken);

            if (!moved)
            {
                // Fallo crítico de I/O en red al mover el archivo: devolver false para reintentar o enviar a DLQ
                logger.LogError(null, LogsConstants.ERROR_FILE_MOVE_FAILED, docEvent.FileName, targetBasePath);
                return false;
            }

            if (dispatchSuccess)
            {
                logger.LogInfo(
                    LogsConstants.INFO_MESSAGE_PROCESSED_SUCCESS,
                    docEvent.FileName,
                    docEvent.PolizaId
                );
            }

            return true;
        }
        catch (Exception ex)
        {
            // Error técnico no controlado (red caída, timeout, etc.): retornamos FALSE para NACK
            logger.LogError(ex, LogsConstants.ERROR_DOCUMENT_PROCESSING_FAILED, docEvent.FileName, ex.Message);
            return false;
        }
    }

    private async Task<bool> EnsureRoutesLoadedAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedRutaFirmados) &&
            !string.IsNullOrWhiteSpace(_cachedRutaNotificados) &&
            !string.IsNullOrWhiteSpace(_cachedRutaErrorNoNotificados))
        {
            return true;
        }

        await _routeLock.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedRutaFirmados) &&
                !string.IsNullOrWhiteSpace(_cachedRutaNotificados) &&
                !string.IsNullOrWhiteSpace(_cachedRutaErrorNoNotificados))
            {
                return true;
            }

            IEnumerable<Parametros> routeParams = await repository.GetConsumerPathsDataAsync();
            var routeList = routeParams.ToList();

            if (routeList.Count == 0)
            {
                logger.LogError(null, LogsConstants.ERROR_NOT_FOUND_CONFIG_SECTION, LogsConstants.ARG_DB_ROUTES_NAME);
                return false;
            }

            _cachedRutaFirmados = routeList.FirstOrDefault(p => string.Equals(p.Clave?.Trim(), SettingsConstants.DB_PARAM_FIRMADOS, StringComparison.OrdinalIgnoreCase))?.Parametro?.Trim() ?? string.Empty;
            _cachedRutaNotificados = routeList.FirstOrDefault(p => string.Equals(p.Clave?.Trim(), SettingsConstants.DB_PARAM_NOTIFICADOS, StringComparison.OrdinalIgnoreCase))?.Parametro?.Trim() ?? string.Empty;
            _cachedRutaErrorNoNotificados = routeList.FirstOrDefault(p => string.Equals(p.Clave?.Trim(), SettingsConstants.DB_PARAM_ERROR_NO_NOTIFICADOS, StringComparison.OrdinalIgnoreCase))?.Parametro?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_cachedRutaFirmados) || string.IsNullOrWhiteSpace(_cachedRutaNotificados) || string.IsNullOrWhiteSpace(_cachedRutaErrorNoNotificados))
            {
                logger.LogError(null, LogsConstants.ERROR_MISSING_ROUTE_KEY, LogsConstants.ARG_DB_ROUTES_MISSING_KEYS);
                return false;
            }

            bool directoriesPrepared = false;
            await networkConnection.RunInNetworkScope(fileStore =>
            {
                directoriesPrepared = storageService.EnsureNetworkDirectoriesExist(fileStore, routeList);
            });

            if (!directoriesPrepared)
            {
                return false;
            }

            if (_isFirstRun)
            {
                logger.LogInfo(LogsConstants.INFO_LOADED_ROUTES, _cachedRutaFirmados, _cachedRutaNotificados, _cachedRutaErrorNoNotificados);
                _isFirstRun = false;
            }

            return true;
        }
        finally
        {
            _routeLock.Release();
        }
    }
}

public sealed record DocumentProcessedEvent(
    long PolizaId,
    string FileName,
    string Status,
    DateTime ProcessedAt
);