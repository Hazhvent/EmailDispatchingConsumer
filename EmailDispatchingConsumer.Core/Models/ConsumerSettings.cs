namespace EmailDispatchingConsumer.Core.Models
{
    public sealed record ConsumerSettings
    {
        public string LogDirectoryPath { get; init; } = null!;
    }
}