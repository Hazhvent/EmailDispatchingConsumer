using EmailDispatchingConsumer.Core.Dto;
using SMBLibrary.Client;

namespace EmailDispatchingConsumer.Core.Interfaces.Services;

public interface IStorageService
{
    /// <summary>
    /// Lista los nombres de archivos PDF (.pdf) presentes en un directorio SMB.
    /// </summary>
    List<string> GetFilesInDirectory(ISMBFileStore fileStore, string directoryPath);

    /// <summary>
    /// Lee un archivo PDF desde la NAS remota por fragmentos a memoria RAM.
    /// </summary>
    byte[]? ReadNetworkFileBytes(ISMBFileStore fileStore, string smbFilePath);

    /// <summary>
    /// Traslada un archivo en el NAS (escribe en destino y elimina de origen).
    /// </summary>
    bool MoveNetworkFile(ISMBFileStore fileStore, string sourceFilePath, string destinationFilePath, byte[] fileBytes);

    /// <summary>
    /// Valida la existencia de un directorio en la red SMB.
    /// </summary>
    bool CheckNetworkDirectoryExists(ISMBFileStore fileStore, string relativePath);

    /// <summary>
    /// Asegura la existencia recursiva de la estructura de directorios remotos.
    /// </summary>
    bool EnsureNetworkDirectoriesExist(ISMBFileStore fileStore, IEnumerable<Parametros> routeConfigurations);
}