namespace EmailDispatchingConsumer.Core.Dto
{
    public class UsuarioInformado
    {
        public string Nombres { get; set; } = string.Empty;
        public int IdTipoDocumento { get; set; }
        public string DocumentoIdentidad { get; set; } = string.Empty;
        public int Sexo { get; set; }
        public string Correo { get; set; } = string.Empty;
    }
}