using Dapper;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace EmailDispatchingConsumer.Infrastructure.Persistence;

public class OracleParameters : SqlMapper.IDynamicParameters
{
    // 1. Contenedor nativo de Dapper para los parámetros estándar (int, string, etc.)
    private readonly DynamicParameters _dynamicParameters = new();

    // 2. Lista interna para capturar parámetros nativos de Oracle (como RefCursors)
    private readonly List<OracleParameter> _oracleParameters = [];

    /// Absorbe todas las propiedades de un objeto de C# (DTO u objeto anónimo) y las convierte en parámetros estándar.
    public void AddDynamicParams(object param)
    {
        _dynamicParameters.AddDynamicParams(param);
    }

    /// Permite agregar manualmente parámetros específicos de Oracle usando el enum oficial OracleDbType.
    public void Add(string name, OracleDbType dbType, ParameterDirection direction, object? value = null, int? size = null)
    {
        var oracleParameter = new OracleParameter(name, dbType, value, direction);
        if (size.HasValue)
        {
            oracleParameter.Size = size.Value;
        }
        _oracleParameters.Add(oracleParameter);
    }

    /// Se dispara automáticamente antes de ejecutar el Stored Procedure.
    public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
    {
        // A. Primero le decimos a Dapper que inyecte los parámetros del objeto de C#
        ((SqlMapper.IDynamicParameters)_dynamicParameters).AddParameters(command, identity);

        // B. Segundo, interceptamos el comando de Oracle, activamos BindByName y agregamos los RefCursors
        if (command is OracleCommand oracleCommand)
        {
            oracleCommand.BindByName = true;
            oracleCommand.Parameters.AddRange(_oracleParameters.ToArray());
        }
    }
}