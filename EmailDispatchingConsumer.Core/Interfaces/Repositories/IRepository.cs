using EmailDispatchingConsumer.Core.Dto;

namespace EmailDispatchingConsumer.Core.Interfaces.Repositories;

public interface IRepository
{
    Task<IEnumerable<Parametros>> GetConsumerPathsDataAsync();
    Task<DatoExterno?> GetDatoExternoAsync(string nombreDoc);
    Task<IEnumerable<NotificationSourceData>> GetInfoClientesExternoAsync(string lote);
}