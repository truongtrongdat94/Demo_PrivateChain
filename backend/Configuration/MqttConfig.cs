namespace HashAnchorDemo.Configuration;

public sealed class MqttConfig
{
    public const string SectionName = "Mqtt";

    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 1883;

    public string Topic { get; init; } = "SmartEMS/WaterQuality/+/Reading";

    public string? Username { get; init; }

    public string? Password { get; init; }
}
