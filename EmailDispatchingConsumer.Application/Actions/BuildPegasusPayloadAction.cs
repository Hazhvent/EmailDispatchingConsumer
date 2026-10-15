namespace EmailDispatchingConsumer.Application.Actions;

using EmailDispatchingConsumer.Core.Constants.Consumer;
using EmailDispatchingConsumer.Core.Constants.Messages;
using EmailDispatchingConsumer.Core.Dto;
using EmailDispatchingConsumer.Core.Interfaces.Repositories;
using EmailDispatchingConsumer.Core.Interfaces.Services;
using MediatR;
using System.Text;

public sealed record BuildPegasusPayloadActionRequest(
    string FileName
) : IRequest<TramaEnvio?>;

public class BuildPegasusPayloadActionHandler(
    IRepository repository,
    ILoggingService<BuildPegasusPayloadActionHandler> logger)
    : IRequestHandler<BuildPegasusPayloadActionRequest, TramaEnvio?>
{
    public async Task<TramaEnvio?> Handle(BuildPegasusPayloadActionRequest request, CancellationToken cancellationToken)
    {
        logger.LogInfo(LogsConstants.INFO_TRAMA_BUILD_STARTED, request.FileName);

        // 1. Obtener información base (Lote y tipo de documento)
        DatoExterno? datoExterno = await repository.GetDatoExternoAsync(request.FileName);

        if (datoExterno == null)
        {
            logger.LogError(null, LogsConstants.ERROR_ORACLE_METADATA_NOT_FOUND, request.FileName);
            return null;
        }

        // 🚨 Regla estricta: Solo se permite procesar flujos de Lote Externo
        if (datoExterno.Externo != 1)
        {
            logger.LogError(null, LogsConstants.ERROR_FILE_NOT_IN_EXTERNAL_BATCH, request.FileName);
            return null;
        }

        // 2. Extraer datos del cliente (Exclusivo Lote Externo) usando el nuevo DTO
        IEnumerable<NotificationSourceData> clientesExternos = await repository.GetInfoClientesExternoAsync(datoExterno.Lote);
        var listaClientes = clientesExternos.ToList();

        if (listaClientes.Count == 0)
        {
            logger.LogError(null, LogsConstants.ERROR_ORACLE_METADATA_NOT_FOUND, request.FileName);
            return null;
        }

        // 🛡️ Búsqueda estricta y determinista: se elimina el fallback '?? listaClientes.FirstOrDefault()'
        // para garantizar que nunca se notifique una póliza a un destinatario ajeno.
        NotificationSourceData? metadata = listaClientes.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.Documentos) &&
            c.Documentos.Split(SettingsConstants.CARACTER_SEPARADOR_DOCUMENTOS)
                  .Any(d => string.Equals(d.Trim(), request.FileName, StringComparison.OrdinalIgnoreCase))
        );

        if (metadata == null)
        {
            logger.LogError(null, LogsConstants.ERROR_ORACLE_METADATA_NOT_FOUND, request.FileName);
            return null;
        }

        // 3. Aplicar lógicas de negocio (Usando metadata.Apellido e IdCertificado)
        string nombreAseguradoFull = CargaNombre(metadata.Nombre, metadata.Apellido);

        // Manejo del valor vacío/defecto que devuelve Oracle usando constantes
        string productoLimpio = (metadata.Producto?.Trim() == SettingsConstants.VALOR_BD_VACIO_DEFAULT)
            ? SettingsConstants.TITULO_PRODUCTO_DEFAULT
            : (metadata.Producto ?? string.Empty);

        int tipoProducto = CargaTipoProducto(productoLimpio);
        int tipoEnvio = CargaTipoEnvio(metadata.IdCertificado.ToString(), tipoProducto);
        int sexo = CargaSexo(metadata.Nombre);
        int tipoDocumento = CargaTipoDocumento(metadata.Dni ?? string.Empty);

        string reglasAplicadas = string.Join(PayloadConstants.COMA_ESPACIO,
            PayloadConstants.RULE_CARGA_NOMBRE,
            PayloadConstants.RULE_CARGA_TIPO_PRODUCTO,
            PayloadConstants.RULE_CARGA_TIPO_ENVIO,
            PayloadConstants.RULE_CARGA_SEXO,
            PayloadConstants.RULE_CARGA_TIPO_DOCUMENTO);

        logger.LogInfo(LogsConstants.INFO_TRAMA_RULES_APPLIED, reglasAplicadas);

        // 4. Ensamblar el Payload (TramaEnvio)
        var tramaEnvio = new TramaEnvio
        {
            Notificacion = new Notificacion
            {
                Titulo = productoLimpio,
                NumeroPoliza = metadata.IdPoliza.ToString(),
                NumeroCertificado = metadata.IdCertificado.ToString(),
                NombreContratante = metadata.Contratante ?? string.Empty,
                NombreAsegurado = nombreAseguradoFull,
                DatosAdicionales = string.Empty,
                Correlativo = string.Empty,
                TipoProductoProtecta = tipoProducto
            },
            Documentos = [new Documento { nombre_documento = request.FileName }],
            UsuariosInformados = [
                new UsuarioInformado
                {
                    Nombres = nombreAseguradoFull,
                    IdTipoDocumento = tipoDocumento,
                    DocumentoIdentidad = metadata.Dni ?? string.Empty,
                    Sexo = sexo,
                    Correo = metadata.Correo ?? string.Empty
                }
            ],
            TipoEnvio = tipoEnvio
        };

        logger.LogInfo(LogsConstants.INFO_TRAMA_BUILT_SUCCESS, metadata.IdPoliza);

        return tramaEnvio;
    }

    // =========================================================
    // 🧮 REGLAS DE NEGOCIO PRIVADAS LEGACY (OPTIMIZADAS)
    // =========================================================
    private static int CargaSexo(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return PayloadConstants.SEXO_MASCULINO;

        string[] vs = nombre.Split(PayloadConstants.ESPACIO, StringSplitOptions.RemoveEmptyEntries);

        if (vs.Length > 0)
        {
            if (vs[0] == PayloadConstants.SALUDO_ESTIMADO) return PayloadConstants.SEXO_MASCULINO;
            if (vs[0] == PayloadConstants.SALUDO_ESTIMADA) return PayloadConstants.SEXO_FEMENINO;
        }

        return PayloadConstants.SEXO_MASCULINO;
    }

    private static string CargaNombre(string? nombre, string? apellido)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return string.Empty;

        string[] vs = nombre.Split(PayloadConstants.ESPACIO, StringSplitOptions.RemoveEmptyEntries);
        string[] va = !string.IsNullOrWhiteSpace(apellido)
            ? apellido.Split(PayloadConstants.ESPACIO, StringSplitOptions.RemoveEmptyEntries)
            : [];

        int startIndex = 0;

        if (vs.Length > 0 && (vs[0] == PayloadConstants.SALUDO_ESTIMADO || vs[0] == PayloadConstants.SALUDO_ESTIMADA))
        {
            startIndex = (vs.Length > 1 && (vs[1].StartsWith(PayloadConstants.SALUDO_SR) || vs[1].StartsWith(PayloadConstants.SALUDO_SRA)))
                ? 2
                : 1;
        }

        var sb = new StringBuilder();

        for (int i = startIndex; i < vs.Length; i++)
        {
            sb.Append(vs[i]).Append(PayloadConstants.ESPACIO);
        }

        for (int i = 0; i < va.Length; i++)
        {
            sb.Append(va[i]).Append(PayloadConstants.ESPACIO);
        }

        return sb.ToString().Trim();
    }

    private static int CargaTipoDocumento(string documento)
    {
        if (string.IsNullOrWhiteSpace(documento)) return PayloadConstants.TIPO_DOC_OTROS;

        return documento.Length switch
        {
            8 => PayloadConstants.TIPO_DOC_DNI,
            11 => PayloadConstants.TIPO_DOC_RUC,
            _ => PayloadConstants.TIPO_DOC_OTROS
        };
    }

    private static int CargaTipoEnvio(string certificado, int tipoProducto)
    {
        if (tipoProducto < 6)
        {
            bool resultadoCert = int.TryParse(certificado, out int cert);
            if (!resultadoCert) cert = 0;
            return cert > 1 ? 4 : 1;
        }

        return tipoProducto switch
        {
            6 => 4,
            7 or 8 or 9 or 10 => 7,
            _ => 1
        };
    }

    private static int CargaTipoProducto(string titulo)
    {
        return titulo switch
        {
            PayloadConstants.PROD_ACCIDENTES_PERSONALES => 1,
            PayloadConstants.PROD_VIDA_GRUPO => 2,
            PayloadConstants.PROD_DESGRAVAMEN => 3,
            PayloadConstants.PROD_VIDA_LEY => 4,
            PayloadConstants.PROD_SCTR => 5,
            PayloadConstants.PROD_SEGURO_SEPELIO => 6,
            PayloadConstants.PROD_VIDA_DEVOLUCION => 7,
            PayloadConstants.PROD_AHORRO_TOTAL => 8,
            PayloadConstants.PROD_RENTA_TOTAL => 9,
            PayloadConstants.PROD_RENTAS_VITALICIAS => 10,
            _ => 11
        };
    }
}