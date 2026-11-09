namespace EmailDispatchingConsumer.Core.Models
{
    public sealed record ApiPegasusSettings
    {
        public string NotificacionUrl { get; init; } = null!;
        public string TokenUrl { get; init; } = null!;
        public string AuthHeader { get; init; } = null!;
        public string Username { get; init; } = null!;
        public string Password { get; init; } = null!;
    }
}