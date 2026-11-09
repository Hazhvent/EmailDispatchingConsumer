namespace EmailDispatchingConsumer.Application.Actions;

using EmailDispatchingConsumer.Core.Interfaces.Services;
using MediatR;

public sealed record ReadDocumentBytesActionRequest(
    string FilePath
) : IRequest<byte[]?>;

public class ReadDocumentBytesActionHandler(
    INetworkConnectionService networkConnection,
    IStorageService storageService)
    : IRequestHandler<ReadDocumentBytesActionRequest, byte[]?>
{
    public async Task<byte[]?> Handle(
        ReadDocumentBytesActionRequest request,
        CancellationToken cancellationToken)
    {
        byte[]? fileBytes = null;

        await networkConnection.RunInNetworkScope(fileStore =>
        {
            fileBytes = storageService.ReadNetworkFileBytes(fileStore, request.FilePath);
        });

        return fileBytes;
    }
}