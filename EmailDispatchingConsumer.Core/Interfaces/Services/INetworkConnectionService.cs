using SMBLibrary.Client;

namespace EmailDispatchingConsumer.Core.Interfaces.Services
{
    public interface INetworkConnectionService
    {
        // 🟢 Método para la infraestructura fija (Conexión predeterminada a la NAS base)
        Task RunInNetworkScope(Action<ISMBFileStore> action);
    }
}