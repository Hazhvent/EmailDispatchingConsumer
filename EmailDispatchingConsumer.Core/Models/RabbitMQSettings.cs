namespace DigitalSigningTaskRunner.Core.Models
{
    // [no tocar 🔒]
    public sealed record RabbitMQSettings
    {
        public string Host { get; init; } = null!;
        public int Port { get; init; }
        public string VirtualHost { get; init; } = null!;
        public string Username { get; init; } = null!;
        public string Password { get; init; } = null!;
        public string ExchangeName { get; init; } = null!;
        public string RoutingKey { get; init; } = null!;
        public string QueueName { get; init; } = null!;
    }
}