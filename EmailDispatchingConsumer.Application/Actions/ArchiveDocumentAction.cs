using EmailDispatchingConsumer.Core.Constants.Consumer;
using EmailDispatchingConsumer.Core.Constants.Messages;
using EmailDispatchingConsumer.Core.Interfaces.Services;
using MediatR;

namespace EmailDispatchingConsumer.Application.Actions;

public sealed record ArchiveDocumentActionRequest(
    string SourceFilePath,
    string TargetBaseDirectory,
    string FileName,
    byte[]? FileBytes
) : IRequest<bool>;

public class ArchiveDocumentActionHandler(
    ISender sender,
    ILoggingService<ArchiveDocumentActionHandler> logger)
    : IRequestHandler<ArchiveDocumentActionRequest, bool>
{
    public async Task<bool> Handle(ArchiveDocumentActionRequest request, CancellationToken cancellationToken)
    {
        string dailyFolder = DateTime.Now.ToString(SettingsConstants.DATE_FORMAT_DAILY_FOLDER);
        string targetDirectory = Path.Combine(request.TargetBaseDirectory, dailyFolder);
        string destinationPath = Path.Combine(targetDirectory, request.FileName);

        bool moved = await sender.Send(new RelocateDocumentActionRequest(
            SourceFilePath: request.SourceFilePath,
            DestinationDirectory: targetDirectory,
            FileName: request.FileName,
            FileBytes: request.FileBytes
        ), cancellationToken);

        if (moved)
        {
            logger.LogInfo(LogsConstants.INFO_FILE_MOVED_SUCCESS, request.FileName, destinationPath);
        }
        else
        {
            logger.LogError(null, LogsConstants.ERROR_FILE_MOVE_FAILED, request.FileName, destinationPath);
        }

        return moved;
    }
}