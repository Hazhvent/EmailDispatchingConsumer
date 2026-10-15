using EmailDispatchingConsumer.Core.Constants.Database;
using EmailDispatchingConsumer.Core.Dto;
using EmailDispatchingConsumer.Core.Interfaces.Repositories;
using System.Data;

namespace EmailDispatchingConsumer.Infrastructure.Persistence.Repositories;

public class Repository(IDbConnection dbConnection) : IRepository
{
    private readonly IDbConnection _dbConnection = dbConnection;

    public async Task<IEnumerable<Parametros>> GetConsumerPathsDataAsync()
    {
        var parametros = new Dictionary<string, object>();

        return await _dbConnection.QueryOracleAsync<Parametros>(
            ProceduresConstants.GET_CONSUMER_PATHS_DATA,
            parametros,
            ParametersConstants.REF_CURSOR
        );
    }

    public async Task<DatoExterno?> GetDatoExternoAsync(string nombreDoc)
    {
        var parametros = new Dictionary<string, object>
        {
            { ParametersConstants.NOMBRE_DOC, nombreDoc }
        };

        return await _dbConnection.QueryOracleFirstOrDefaultAsync<DatoExterno>(
            ProceduresConstants.SP_GETDATOEXTERNO,
            parametros,
            ParametersConstants.CURSOR_RET
        );
    }

    public async Task<IEnumerable<NotificationSourceData>> GetInfoClientesExternoAsync(string lote)
    {
        var parametros = new Dictionary<string, object>
        {
            { ParametersConstants.PS_LOTE, lote }
        };

        return await _dbConnection.QueryOracleAsync<NotificationSourceData>(
            ProceduresConstants.GET_CONSUMER_DATA,
            parametros,
            ParametersConstants.CURSOR_RET
        );
    }
}