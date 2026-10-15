namespace EmailDispatchingConsumer.Core.Dto;

public class NotificationSourceData
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string Documentos { get; set; } = string.Empty;
    public long IdPoliza { get; set; }
    public string Contratante { get; set; } = string.Empty;
    public string Asegurado { get; set; } = string.Empty;
    public long IdCertificado { get; set; }
    public string Producto { get; set; } = string.Empty;
}