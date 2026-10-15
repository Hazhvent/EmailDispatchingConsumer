using EmailDispatchingConsumer.Core.Constants.Messages;
using EmailDispatchingConsumer.Core.Interfaces.Services;
using EmailDispatchingConsumer.Core.Models;
using Microsoft.Extensions.Options;
using SMBLibrary;
using SMBLibrary.Client;
using System.Net;
using System.Net.Sockets;

namespace EmailDispatchingConsumer.Infrastructure.Services;

public sealed class NetworkConnectionService(
    IOptions<NASSettings> nasOptions,
    ILoggingService<NetworkConnectionService> logger) : INetworkConnectionService
{
    private readonly NASSettings _nasSettings = nasOptions.Value;

    // 🟢 ÁMBITO FIJO: Conexión predeterminada a la NAS base (appsettings.json)
    public async Task RunInNetworkScope(Action<ISMBFileStore> action)
    {
        await Task.Run(() =>
        {
            SMB2Client client = new();
            ISMBFileStore? fileStore = null;

            try
            {
                fileStore = EstablishConnection(client, _nasSettings.Host, _nasSettings.ShareFolder);
                if (fileStore != null)
                {
                    action(fileStore);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, LogsConstants.ERROR_SMB_GENERIC, ex.Message);
            }
            finally
            {
                fileStore?.Disconnect();
                if (client.IsConnected)
                {
                    try { client.Logoff(); } catch { }
                    try { client.Disconnect(); } catch { }
                }
            }
        });
    }

    private ISMBFileStore? EstablishConnection(SMB2Client client, string hostName, string shareName)
    {
        string username = _nasSettings.Username;
        string password = _nasSettings.Password;
        string domain = _nasSettings.Domain ?? string.Empty;

        IPAddress ip;
        try
        {
            if (!IPAddress.TryParse(hostName, out ip!))
            {
                IPAddress[] ips = Dns.GetHostAddresses(hostName);
                ip = ips.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork)
                       ?? ips.First();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, LogsConstants.ERROR_SMB_HOST_NOT_FOUND, hostName);
            return null;
        }

        bool isConnected = client.Connect(ip, SMBTransportType.DirectTCPTransport);
        if (!isConnected)
        {
            logger.LogError(null, LogsConstants.ERROR_SMB_TIMEOUT);
            return null;
        }

        NTStatus loginStatus = client.Login(domain, username, password);
        if (loginStatus != NTStatus.STATUS_SUCCESS)
        {
            logger.LogError(null, LogsConstants.ERROR_SMB_AUTH_CREDENTIALS_INVALID);
            return null;
        }

        ISMBFileStore fileStore = client.TreeConnect(shareName, out NTStatus treeStatus);
        if (treeStatus != NTStatus.STATUS_SUCCESS)
        {
            switch (treeStatus)
            {
                case NTStatus.STATUS_ACCESS_DENIED:
                    logger.LogError(null, LogsConstants.ERROR_SMB_AUTH_READ_DENIED);
                    break;
                case NTStatus.STATUS_BAD_NETWORK_NAME:
                    logger.LogError(null, LogsConstants.ERROR_PATH_NOT_EXISTS, shareName);
                    break;
                default:
                    logger.LogError(null, LogsConstants.ERROR_SMB_GENERIC, treeStatus);
                    break;
            }
            return null;
        }

        return fileStore;
    }
}