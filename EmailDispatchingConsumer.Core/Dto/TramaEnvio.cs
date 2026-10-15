namespace EmailDispatchingConsumer.Core.Dto
{
    public class TramaEnvio
    {
        public Notificacion Notificacion { get; set; } = new();
        public int TipoEnvio { get; set; }
        public List<Documento> Documentos { get; set; } = [];
        public List<UsuarioInformado> UsuariosInformados { get; set; } = [];
    }
}