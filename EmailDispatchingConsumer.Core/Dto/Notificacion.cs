namespace EmailDispatchingConsumer.Core.Dto
{
    public class Notificacion
    {
        public string Titulo { get; set; } = string.Empty;
        public string NumeroPoliza { get; set; } = string.Empty;
        public string NumeroCertificado { get; set; } = string.Empty;
        public string NombreContratante { get; set; } = string.Empty;
        public string NombreAsegurado { get; set; } = string.Empty;
        public string DatosAdicionales { get; set; } = string.Empty;
        public string Correlativo { get; set; } = string.Empty;
        public int TipoProductoProtecta { get; set; }
    }
}