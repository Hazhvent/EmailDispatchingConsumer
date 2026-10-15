namespace EmailDispatchingConsumer.Core.Models
{
    // [no tocar 🔒]
    public sealed record NASSettings // uso de null! por principio de fail-fast
    {
        public string Host { get; init; } = null!;
        public string Domain { get; init; } = null!;
        public string Username { get; init; } = null!;
        public string Password { get; init; } = null!;
        public string ShareFolder { get; init; } = null!;
    }
}