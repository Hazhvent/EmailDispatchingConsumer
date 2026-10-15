namespace EmailDispatchingConsumer.Application.Actions;

using EmailDispatchingConsumer.Core.Dto;
using EmailDispatchingConsumer.Core.Interfaces.Services;
using MediatR;

public sealed record RelocateDocumentActionRequest(
    string SourceFilePath,
    string DestinationDirectory,
    string FileName,
    byte[]? FileBytes = null
) : IRequest<bool>;

public class RelocateDocumentActionHandler(
    INetworkConnectionService networkConnection,
    IStorageService storageService)
    : IRequestHandler<RelocateDocumentActionRequest, bool>
{
    public async Task<bool> Handle(
        RelocateDocumentActionRequest request,
        CancellationToken cancellationToken)
    {
        string destinationFilePath = Path.Combine(request.DestinationDirectory, request.FileName);
        bool moveSuccess = false;

        await networkConnection.RunInNetworkScope(fileStore =>
        {
            // 1. Asegurar existencia de la subcarpeta destino en el NAS
            var targetParam = new List<Parametros>
            {
                new() { Parametro = request.DestinationDirectory }
            };
            storageService.EnsureNetworkDirectoriesExist(fileStore, targetParam);

            // 2. Si no vienen los bytes en memoria, leerlos antes de trasladar
            byte[]? activeBytes = request.FileBytes ?? storageService.ReadNetworkFileBytes(fileStore, request.SourceFilePath);

            if (activeBytes != null && activeBytes.Length > 0)
            {
                moveSuccess = storageService.MoveNetworkFile(
                    fileStore,
                    request.SourceFilePath,
                    destinationFilePath,
                    activeBytes
                );
            }
        });

        return moveSuccess;
    }
}