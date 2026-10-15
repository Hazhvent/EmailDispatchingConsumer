namespace EmailDispatchingConsumer.Infrastructure.Services;

using EmailDispatchingConsumer.Core.Constants.Consumer;
using EmailDispatchingConsumer.Core.Constants.Messages;
using EmailDispatchingConsumer.Core.Dto;
using EmailDispatchingConsumer.Core.Interfaces.Services;
using SMBLibrary;
using SMBLibrary.Client;

public sealed class StorageService(ILoggingService<StorageService> logger) : IStorageService
{
    // =========================================================================
    // 🔎 DESCUBRIMIENTO DE ARCHIVOS PDF
    // =========================================================================
    public List<string> GetFilesInDirectory(ISMBFileStore fileStore, string directoryPath)
    {
        var fileNames = new List<string>();

        NTStatus status = fileStore.CreateFile(
            out object directoryHandle,
            out _,
            directoryPath,
            AccessMask.GENERIC_READ,
            SMBLibrary.FileAttributes.Directory,
            ShareAccess.Read | ShareAccess.Write | ShareAccess.Delete,
            CreateDisposition.FILE_OPEN,
            CreateOptions.FILE_DIRECTORY_FILE,
            null);

        if (status != NTStatus.STATUS_SUCCESS)
        {
            logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, status);
            return fileNames;
        }

        try
        {
            NTStatus queryStatus = fileStore.QueryDirectory(
                out List<QueryDirectoryFileInformation> fileList,
                directoryHandle,
                SettingsConstants.SMB_ALL_FILES_PATTERN,
                FileInformationClass.FileDirectoryInformation);

            if ((queryStatus == NTStatus.STATUS_SUCCESS || queryStatus == NTStatus.STATUS_NO_MORE_FILES) && fileList != null)
            {
                foreach (var fileInfo in fileList)
                {
                    if (fileInfo is FileDirectoryInformation dirInfo)
                    {
                        string fileName = dirInfo.FileName;
                        bool isDir = dirInfo.FileAttributes.HasFlag(SMBLibrary.FileAttributes.Directory);

                        if (fileName == SettingsConstants.SMB_DIRECTORY_CURRENT ||
                            fileName == SettingsConstants.SMB_DIRECTORY_PARENT ||
                            isDir)
                        {
                            continue;
                        }

                        if (fileName.EndsWith(SettingsConstants.PDF_EXTENSION, StringComparison.OrdinalIgnoreCase))
                        {
                            fileNames.Add(fileName);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, LogsConstants.ERROR_SMB_GENERIC, ex.Message);
        }
        finally
        {
            fileStore.CloseFile(directoryHandle);
        }

        return fileNames;
    }

    // =========================================================================
    // 📥 LECTURA BINARIA (RAM)
    // =========================================================================
    public byte[]? ReadNetworkFileBytes(ISMBFileStore fileStore, string smbFilePath)
    {
        if (string.IsNullOrWhiteSpace(smbFilePath)) return null;

        try
        {
            NTStatus status = fileStore.CreateFile(
                out object fileHandle,
                out _,
                smbFilePath,
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                switch (status)
                {
                    case NTStatus.STATUS_SHARING_VIOLATION:
                        logger.LogError(null, LogsConstants.ERROR_SMB_FILE_LOCKED, smbFilePath);
                        break;
                    case NTStatus.STATUS_ACCESS_DENIED:
                        logger.LogError(null, LogsConstants.ERROR_SMB_AUTH_READ_DENIED);
                        break;
                    case NTStatus.STATUS_OBJECT_NAME_NOT_FOUND:
                    case NTStatus.STATUS_OBJECT_PATH_NOT_FOUND:
                        logger.LogError(null, LogsConstants.ERROR_PATH_NOT_EXISTS, smbFilePath);
                        break;
                    default:
                        logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, status);
                        break;
                }
                return null;
            }

            try
            {
                NTStatus infoStatus = fileStore.GetFileInformation(out FileInformation fileInfo, fileHandle, FileInformationClass.FileStandardInformation);

                if (infoStatus != NTStatus.STATUS_SUCCESS)
                {
                    logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, infoStatus);
                    return null;
                }

                long fileSize = (fileInfo as FileStandardInformation)?.EndOfFile ?? 0;

                using MemoryStream ms = new();
                long bytesLeidos = 0;

                while (bytesLeidos < fileSize)
                {
                    int bytesToRead = CalculateNextChunkSize(fileSize, bytesLeidos, SettingsConstants.SMB_READ_CHUNK_SIZE);
                    NTStatus readStatus = fileStore.ReadFile(out byte[] buffer, fileHandle, bytesLeidos, bytesToRead);

                    if (readStatus != NTStatus.STATUS_SUCCESS && readStatus != NTStatus.STATUS_END_OF_FILE)
                    {
                        logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, readStatus);
                        return null;
                    }

                    if (buffer != null && buffer.Length > 0)
                    {
                        ms.Write(buffer, 0, buffer.Length);
                        bytesLeidos += buffer.Length;
                    }
                    else
                    {
                        break;
                    }
                }

                return ms.ToArray();
            }
            finally
            {
                fileStore.CloseFile(fileHandle);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, LogsConstants.ERROR_SMB_GENERIC, ex.Message);
            return null;
        }
    }

    // =========================================================================
    // 🚚 TRASLADO PÚBLICO: Valida integridad en destino antes de borrar el origen
    // =========================================================================
    public bool MoveNetworkFile(ISMBFileStore fileStore, string sourceFilePath, string destinationFilePath, byte[] fileBytes)
    {
        // 1. Escritura física en la ruta destino
        bool written = WriteNetworkFilePrivate(fileStore, destinationFilePath, fileBytes);
        if (!written) return false;

        // 2. 🛡️ Verificación de integridad: el archivo destino debe coincidir exactamente en tamaño
        bool isIntegrityValid = ValidateFileSizePrivate(fileStore, destinationFilePath, fileBytes.Length);
        if (!isIntegrityValid)
        {
            logger.LogError(null, LogsConstants.ERROR_SMB_FILE_INTEGRITY_FAILED, destinationFilePath, fileBytes.Length);

            // Limpiar el archivo truncado/corrupto en destino para no dejar basura en el NAS
            DeleteNetworkFilePrivate(fileStore, destinationFilePath);
            return false;
        }

        // 3. Eliminación segura del origen solo tras certificar la copia íntegra
        return DeleteNetworkFilePrivate(fileStore, sourceFilePath);
    }

    // =========================================================================
    // 📁 VALIDACIÓN Y ESTRUCTURA DE DIRECTORIOS
    // =========================================================================
    public bool CheckNetworkDirectoryExists(ISMBFileStore fileStore, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;

        try
        {
            NTStatus status = fileStore.CreateFile(
                out object directoryHandle,
                out _,
                relativePath,
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write | ShareAccess.Delete,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            if (status == NTStatus.STATUS_SUCCESS)
            {
                fileStore.CloseFile(directoryHandle);
                return true;
            }

            if (status == NTStatus.STATUS_OBJECT_NAME_NOT_FOUND || status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
            {
                return false;
            }

            switch (status)
            {
                case NTStatus.STATUS_ACCESS_DENIED:
                    logger.LogError(null, LogsConstants.ERROR_SMB_AUTH_READ_DENIED);
                    break;
                case NTStatus.STATUS_SHARING_VIOLATION:
                    logger.LogError(null, LogsConstants.ERROR_SMB_FILE_LOCKED, relativePath);
                    break;
                default:
                    logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, status);
                    break;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, LogsConstants.ERROR_SMB_GENERIC, ex.Message);
            return false;
        }
    }

    public bool EnsureNetworkDirectoriesExist(ISMBFileStore fileStore, IEnumerable<Parametros> routeConfigurations)
    {
        if (routeConfigurations == null || !routeConfigurations.Any()) return true;

        try
        {
            foreach (var routeConfig in routeConfigurations)
            {
                string relativePath = routeConfig.Parametro;

                if (!string.IsNullOrWhiteSpace(relativePath))
                {
                    relativePath = relativePath.TrimStart(SettingsConstants.PATH_SEPARATOR_BACKSLASH, SettingsConstants.PATH_SEPARATOR_SLASH);
                    char[] separadores = { SettingsConstants.PATH_SEPARATOR_BACKSLASH, SettingsConstants.PATH_SEPARATOR_SLASH };
                    string[] segments = relativePath.Split(separadores, StringSplitOptions.RemoveEmptyEntries);

                    string currentPath = string.Empty;

                    foreach (string segment in segments)
                    {
                        currentPath = string.IsNullOrEmpty(currentPath) ? segment : Path.Combine(currentPath, segment);

                        if (!CheckNetworkDirectoryExists(fileStore, currentPath))
                        {
                            NTStatus status = fileStore.CreateFile(
                                out object directoryHandle,
                                out _,
                                currentPath,
                                AccessMask.GENERIC_READ | AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                                SMBLibrary.FileAttributes.Directory,
                                ShareAccess.Read | ShareAccess.Write | ShareAccess.Delete,
                                CreateDisposition.FILE_OPEN_IF,
                                CreateOptions.FILE_DIRECTORY_FILE,
                                null);

                            if (status == NTStatus.STATUS_SUCCESS)
                            {
                                fileStore.CloseFile(directoryHandle);
                            }
                            else
                            {
                                switch (status)
                                {
                                    case NTStatus.STATUS_SHARING_VIOLATION:
                                        logger.LogError(null, LogsConstants.ERROR_SMB_FILE_LOCKED, currentPath);
                                        break;
                                    case NTStatus.STATUS_ACCESS_DENIED:
                                        logger.LogError(null, LogsConstants.ERROR_SMB_AUTH_WRITE_DENIED);
                                        break;
                                    case NTStatus.STATUS_OBJECT_NAME_NOT_FOUND:
                                    case NTStatus.STATUS_OBJECT_PATH_NOT_FOUND:
                                        logger.LogError(null, LogsConstants.ERROR_PATH_NOT_EXISTS, currentPath);
                                        break;
                                    default:
                                        logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, status);
                                        break;
                                }
                                return false;
                            }
                        }
                    }
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, LogsConstants.ERROR_SMB_GENERIC, ex.Message);
            return false;
        }
    }

    // =========================================================================
    // 🔒 UTILIDADES PRIVADAS (Escritura, Verificación, Eliminación y Matemáticas)
    // =========================================================================
    private bool ValidateFileSizePrivate(ISMBFileStore fileStore, string filePath, long expectedSize)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;

        try
        {
            NTStatus status = fileStore.CreateFile(
                out object fileHandle,
                out _,
                filePath,
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, status);
                return false;
            }

            try
            {
                NTStatus infoStatus = fileStore.GetFileInformation(out FileInformation fileInfo, fileHandle, FileInformationClass.FileStandardInformation);
                if (infoStatus != NTStatus.STATUS_SUCCESS)
                {
                    logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, infoStatus);
                    return false;
                }

                long writtenSize = (fileInfo as FileStandardInformation)?.EndOfFile ?? -1;
                return writtenSize == expectedSize;
            }
            finally
            {
                fileStore.CloseFile(fileHandle);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, LogsConstants.ERROR_SMB_GENERIC, ex.Message);
            return false;
        }
    }

    private bool WriteNetworkFilePrivate(ISMBFileStore fileStore, string filePath, byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length == 0 || string.IsNullOrWhiteSpace(filePath))
        {
            logger.LogError(null, LogsConstants.ERROR_INVALID_DATA_FORMAT);
            return false;
        }

        try
        {
            NTStatus status = fileStore.CreateFile(
                out object fileHandle,
                out _,
                filePath,
                AccessMask.GENERIC_WRITE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_SUPERSEDE,
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                switch (status)
                {
                    case NTStatus.STATUS_SHARING_VIOLATION:
                        logger.LogError(null, LogsConstants.ERROR_SMB_FILE_LOCKED, filePath);
                        break;
                    case NTStatus.STATUS_ACCESS_DENIED:
                        logger.LogError(null, LogsConstants.ERROR_SMB_AUTH_WRITE_DENIED);
                        break;
                    default:
                        logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, status);
                        break;
                }
                return false;
            }

            try
            {
                int chunkSize = SettingsConstants.SMB_READ_CHUNK_SIZE;
                long offset = 0;

                while (offset < fileBytes.Length)
                {
                    int bytesToWrite = (int)Math.Min(chunkSize, fileBytes.Length - offset);
                    byte[] chunkBuffer = new byte[bytesToWrite];
                    Buffer.BlockCopy(fileBytes, (int)offset, chunkBuffer, 0, bytesToWrite);

                    NTStatus writeStatus = fileStore.WriteFile(out _, fileHandle, offset, chunkBuffer);
                    if (writeStatus != NTStatus.STATUS_SUCCESS)
                    {
                        logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, writeStatus);
                        return false;
                    }
                    offset += bytesToWrite;
                }

                return true;
            }
            finally
            {
                fileStore.CloseFile(fileHandle);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, LogsConstants.ERROR_SMB_GENERIC, ex.Message);
            return false;
        }
    }

    private bool DeleteNetworkFilePrivate(ISMBFileStore fileStore, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return true;

        try
        {
            NTStatus status = fileStore.CreateFile(
                out object fileHandle,
                out _,
                filePath,
                AccessMask.DELETE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.Read | ShareAccess.Write | ShareAccess.Delete,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_DELETE_ON_CLOSE,
                null);

            if (status == NTStatus.STATUS_OBJECT_NAME_NOT_FOUND || status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
            {
                return true;
            }

            if (status != NTStatus.STATUS_SUCCESS)
            {
                switch (status)
                {
                    case NTStatus.STATUS_SHARING_VIOLATION:
                        logger.LogError(null, LogsConstants.ERROR_SMB_FILE_LOCKED, filePath);
                        break;
                    case NTStatus.STATUS_ACCESS_DENIED:
                        logger.LogError(null, LogsConstants.ERROR_SMB_AUTH_WRITE_DENIED);
                        break;
                    default:
                        logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, status);
                        break;
                }
                return false;
            }

            fileStore.CloseFile(fileHandle);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, LogsConstants.ERROR_SMB_GENERIC, ex.Message);
            return false;
        }
    }

    private int CalculateNextChunkSize(long fileSize, long bytesRead, int maxChunkSize)
    {
        if (bytesRead >= fileSize) return 0;
        return (int)Math.Min(maxChunkSize, fileSize - bytesRead);
    }
}