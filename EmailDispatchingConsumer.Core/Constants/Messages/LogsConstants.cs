namespace EmailDispatchingConsumer.Core.Constants.Messages;

using EmailDispatchingConsumer.Core.Constants.Consumer;

public static class LogsConstants
{
    // ⚙️ PARÁMETROS DE CONFIGURACIÓN DE LOGS (SERILOG)
    public const string LOGGER_FILE_PATH_KEY = $"{SettingsConstants.CONSUMER_SETTINGS_SECTION_NAME}:LogDirectoryPath";
    public const string LOGGER_CATEGORY_MICROSOFT = "Microsoft";
    public const string LOGGER_CATEGORY_MICROSOFT_HOSTING_LIFETIME = "Microsoft.Hosting.Lifetime";
    public const int LOGGER_FILE_RETAINED_COUNT_LIMIT = 30;
    public const string LOGGER_CONSOLE_OUTPUT_TEMPLATE = "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";
    public const string LOGGER_FILE_OUTPUT_TEMPLATE = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    // 🟢 INFO LOGS ENRIQUECIDOS (STRUCTURED LOGGING)
    public const string INFO_LOADED_ROUTES = "📁 Rutas configuradas en BD -> Firmados: '{RutaFirmados}' | Notificados: '{RutaNotificados}' | Error No Notificados: '{RutaErrorNoNotificados}'";
    public const string INFO_PEGASUS_NOTIFICATION_SUCCESS = "📧 Notificación despachada exitosamente en Pegasus/Softnet para el archivo '{FileName}'. Respuesta: {Response}";
    public const string INFO_FILE_MOVED_SUCCESS = "🚚 Archivo '{FileName}' trasladado exitosamente en el NAS a la ruta: '{DestinationPath}'.";
    public const string INFO_MESSAGE_PROCESSED_SUCCESS = "✅ Mensaje procesado y completado exitosamente -> Archivo: '{FileName}' | Póliza: '{NumeroPoliza}'.";
    public const string INFO_WORKER_STARTING = "🚀 Iniciando EmailDispatchingConsumer...";
    public const string INFO_AUTHOR = "👤 Autor: Creado y diseñado por Cesar Galvez.";
    public const string INFO_CONTACT = "📧 Para consultas concretas por favor utilice este correo para comunicarse: hazhvent@hotmail.com.";
    public const string INFO_WORKER_STOP_SIGNAL = "🛑 El consumidor ha recibido la señal de detención.";
    public const string INFO_WORKER_STOPPED = "💤 EmailDispatchingConsumer detenido por completo.";

    // 🟢 NUEVOS LOGS DE CONSTRUCCIÓN DE TRAMA
    public const string INFO_TRAMA_BUILD_STARTED = "📦 Iniciando empaquetado de TramaEnvio para el archivo '{FileName}'.";
    public const string INFO_TRAMA_RULES_APPLIED = "🧮 Reglas de negocio evaluadas correctamente: [{Reglas}].";
    public const string INFO_TRAMA_BUILT_SUCCESS = "✅ TramaEnvio ensamblada exitosamente para la Póliza: '{NumeroPoliza}'.";

    // 🔴 ERROR LOGS
    public const string ERROR_MISSING_ROUTE_KEY = "Falta la configuración de la clave de ruta '{Clave}' en la base de datos (PD_PARAMETROS).";
    public const string ERROR_NOT_FOUND_CONFIG_SECTION = "La sección '{SectionName}' no se encontró o no pudo ser mapeada en la configuración.";
    public const string ERROR_INVALID_DATA_FORMAT = "La estructura interna de los datos no tiene un formato válido o presenta errores de sintaxis.";

    public const string ERROR_PDF_BYTES_EMPTY = "El archivo PDF recibido es nulo o está vacío para el documento '{FileName}'.";
    public const string ERROR_ORACLE_METADATA_NOT_FOUND = "No se encontró información en Oracle (InformacionPoliza) para el archivo '{FileName}'.";
    public const string ERROR_FILE_NOT_IN_EXTERNAL_BATCH = "El archivo '{FileName}' no está registrado en el lote correspondiente.";

    public const string ERROR_PEGASUS_TOKEN_FAILED = "No se pudo obtener el token de autorización OAuth2 de Pegasus/Softnet.";
    public const string ERROR_PEGASUS_TOKEN_HTTP = "Fallo HTTP al solicitar token OAuth2 (Código: {StatusCode}): {Detail}";
    public const string ERROR_PEGASUS_TOKEN_INVALID_RESPONSE = "La respuesta del endpoint de autenticación no contiene la propiedad 'access_token'.";
    public const string ERROR_PEGASUS_NOTIFICATION_HTTP = "Error de respuesta en API de Notificación Pegasus (HTTP {StatusCode}): {Detail}";
    public const string ERROR_PEGASUS_NOTIFICATION_FAILED = "Fallo al despachar la notificación por correo para el archivo '{FileName}': {Detail}";
    public const string ERROR_PEGASUS_API_TIMEOUT = "Se excedió el tiempo de espera (Timeout) al comunicarse con la API de Pegasus.";

    public const string ERROR_PEGASUS_API_REJECTED = "Pegasus API rechazó la notificación. Respuesta: {Mensaje}";
    public const string ERROR_PEGASUS_JSON_PARSE_FAILED = "No se pudo deserializar la respuesta JSON de Pegasus/Softnet. Trama recibida: {Response}";
    public const string ERROR_PEGASUS_NO_DETAIL = "Sin detalle de error";
    public const string ERROR_PEGASUS_UNKNOWN = "Error desconocido en la API";

    public const string ERROR_RABBITMQ_CONSUME = "Fallo de infraestructura en RabbitMQ durante el procesamiento del mensaje: {Detail}";
    public const string ERROR_RABBITMQ_DESERIALIZE = "No se pudo deserializar el contenido del mensaje recibido desde la cola '{QueueName}'.";

    public const string ERROR_WORKER_CRITICAL = "🔥 Error crítico inesperado en el ciclo principal del Consumer: {Message}";
    public const string ERROR_DOCUMENT_PROCESSING_FAILED = "🔥 Error no controlado procesando el despacho del documento '{FileName}': {Message}";
    public const string ERROR_FILE_MOVE_FAILED = "No se pudo trasladar el archivo '{FileName}' a su ruta destino final '{DestinationPath}'.";

    public const string ERROR_SMB_HOST_NOT_FOUND = "Fallo SMB: No se pudo resolver el nombre del host o dirección IP '{HostName}' en la red (DNS / Host desconocido).";
    public const string ERROR_SMB_TIMEOUT = "Fallo SMB: Se agotó el tiempo de espera al comunicarse con el servidor o el puerto SMB (445) no está alcanzable.";
    public const string ERROR_SMB_AUTH_CREDENTIALS_INVALID = "Fallo SMB: El usuario o contraseña especificados en NASSettings son incorrectos.";
    public const string ERROR_SMB_AUTH_READ_DENIED = "Fallo SMB: Acceso denegado. El usuario de red no tiene permisos de LECTURA para inspeccionar o acceder a esta ruta.";
    public const string ERROR_SMB_AUTH_WRITE_DENIED = "Fallo SMB: Acceso denegado. El usuario de red carece de permisos de ESCRITURA para mover archivos en el destino.";
    public const string ERROR_SMB_FILE_LOCKED = "Fallo SMB: La ruta o archivo '{Path}' está bloqueado o en uso por otro proceso (Sharing Violation).";
    public const string ERROR_SMB_FILE_INTEGRITY_FAILED = "Fallo SMB: La verificación de integridad falló para '{DestinationPath}'. El tamaño en destino no coincide con los {ExpectedBytes} bytes esperados.";
    public const string ERROR_SMB_GENERIC = "Fallo SMB: El servidor rechazó la operación (Código: {StatusCode}).";
    public const string ERROR_PATH_NOT_EXISTS = "Fallo SMB: La ruta o directorio '{Path}' especificado no existe en el servidor.";

    // 📌 ARGUMENTOS DE LOGS
    public const string ARG_DB_ROUTES_NAME = "Rutas BD Consumer";
    public const string ARG_DB_ROUTES_MISSING_KEYS = $"{SettingsConstants.DB_PARAM_FIRMADOS}/{SettingsConstants.DB_PARAM_NOTIFICADOS}/{SettingsConstants.DB_PARAM_ERROR_NO_NOTIFICADOS}";
}