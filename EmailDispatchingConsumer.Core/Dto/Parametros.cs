namespace EmailDispatchingConsumer.Core.Dto
{
    public class Parametros
    {
        public int IdParametro { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Parametro { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int Estado { get; set; }
    }
}
