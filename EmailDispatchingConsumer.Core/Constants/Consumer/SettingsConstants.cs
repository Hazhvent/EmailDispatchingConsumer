namespace EmailDispatchingConsumer.Core.Constants.Consumer;

public static class SettingsConstants
{
    // ⚙️ Nombres de secciones de configuración (appsettings.json)
    public const string DB_CONNECTION_STRING_NAME = "Database";
    public const string CONSUMER_SETTINGS_SECTION_NAME = "ConsumerSettings";
    public const string NAS_SETTINGS_SECTION_NAME = "NASSettings";
    public const string API_PEGASUS_SETTINGS_SECTION_NAME = "ApiPegasusSettings";
    public const string RABBITMQ_SETTINGS_SECTION_NAME = "RabbitMQSettings";

    // 🖥️ Identificador del Servicio de Windows
    public const string WINDOWS_SERVICE_NAME = "E-policy - Email Dispatching Consumer";

    // 🌐 Cabeceras, Headers y Contenidos HTTP
    public const string HTTP_HEADER_AUTHORIZATION = "Authorization";
    public const string HTTP_AUTH_BEARER_SCHEME = "Bearer";

    // 🔑 Parámetros y Respuestas OAuth2 (Pegasus / Softnet)
    public const string OAUTH_PARAM_GRANT_TYPE = "grant_type";
    public const string OAUTH_PARAM_USERNAME = "username";
    public const string OAUTH_PARAM_PASSWORD = "password";
    public const string OAUTH_GRANT_TYPE_PASSWORD = "password";
    public const string OAUTH_RESPONSE_ACCESS_TOKEN = "access_token";

    // 👇 Claves de respuesta de la API de Notificación
    public const string PEGASUS_RESPONSE_ESTADO = "estado";
    public const string PEGASUS_RESPONSE_MENSAJE = "mensaje";

    // 💾 Claves de Parámetros de Rutas en Base de Datos (PD_PARAMETROS)
    public const string DB_PARAM_FIRMADOS = "FIRMADOS";
    public const string DB_PARAM_NOTIFICADOS = "NOTIFICADOS";
    public const string DB_PARAM_ERROR_NO_NOTIFICADOS = "ERROR_NO_NOTIFICADOS";

    // 📅 Formatos de Fecha para Estructura de Directorios
    public const string DATE_FORMAT_DAILY_FOLDER = "yyyy-MM-dd";

    // ⚙️ Configuraciones sobre gestión de archivos
    public const string SMB_ALL_FILES_PATTERN = "*";
    public const string PDF_EXTENSION = ".pdf";
    public const int SMB_READ_CHUNK_SIZE = 1024 * 64;

    // 🛠️ Separadores y Punteros de Directorios Remotos (SMB / Red)
    public const string SMB_DIRECTORY_CURRENT = ".";
    public const string SMB_DIRECTORY_PARENT = "..";
    public const char PATH_SEPARATOR_BACKSLASH = '\\';
    public const char PATH_SEPARATOR_SLASH = '/';

    // 📝 Caracteres y cadenas literales utilizados en el flujo
    public const char CARACTER_SEPARADOR_DOCUMENTOS = ';';
    public const string VALOR_BD_VACIO_DEFAULT = "-";
    public const string TITULO_PRODUCTO_DEFAULT = "Póliza de Seguro";
}