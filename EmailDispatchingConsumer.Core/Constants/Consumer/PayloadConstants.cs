namespace EmailDispatchingConsumer.Core.Constants.Consumer;

public static class PayloadConstants
{
    // ==============================================================================
    // 🧮 NOMBRES DE REGLAS DE NEGOCIO (PARA TRAZABILIDAD Y LOGS)
    // ==============================================================================
    public const string RULE_CARGA_NOMBRE = "CargaNombre";
    public const string RULE_CARGA_SEXO = "CargaSexo";
    public const string RULE_CARGA_TIPO_DOCUMENTO = "CargaTipoDocumento";
    public const string RULE_CARGA_TIPO_ENVIO = "CargaTipoEnvio";
    public const string RULE_CARGA_TIPO_PRODUCTO = "CargaTipoProducto";

    // ==============================================================================
    // 📇 REGLAS DE NEGOCIO: SALUDOS Y TRATAMIENTOS
    // ==============================================================================
    public const string SALUDO_ESTIMADO = "Estimado";
    public const string SALUDO_ESTIMADA = "Estimada";
    public const string SALUDO_SR = "Sr";
    public const string SALUDO_SRA = "Sra";
    public const string ESPACIO = " ";
    public const string COMA_ESPACIO = ", ";

    // ==============================================================================
    // 🛡️ REGLAS DE NEGOCIO: NOMBRES DE PRODUCTOS
    // ==============================================================================
    public const string PROD_ACCIDENTES_PERSONALES = "Accidentes Personales";
    public const string PROD_VIDA_GRUPO = "Vida Grupo";
    public const string PROD_DESGRAVAMEN = "Desgravamen";
    public const string PROD_VIDA_LEY = "Vida Ley";
    public const string PROD_SCTR = "SCTR";
    public const string PROD_SEGURO_SEPELIO = "Seguro de Sepelio";
    public const string PROD_VIDA_DEVOLUCION = "Vida Devolución Protecta+";
    public const string PROD_AHORRO_TOTAL = "Ahorro Total Protecta";
    public const string PROD_RENTA_TOTAL = "Renta Total Protecta";
    public const string PROD_RENTAS_VITALICIAS = "Rentas Vitalicias";

    // ==============================================================================
    // 🔢 CÓDIGOS DE MAPEO: SEXO Y TIPO DE DOCUMENTO
    // ==============================================================================
    public const int SEXO_FEMENINO = 1;
    public const int SEXO_MASCULINO = 2;

    public const int TIPO_DOC_DNI = 1;     // Longitud 8
    public const int TIPO_DOC_RUC = 4;     // Longitud 11
    public const int TIPO_DOC_OTROS = 2;   // Por defecto
}