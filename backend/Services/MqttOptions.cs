namespace HashAnchorDemo;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 1883;

    public string Topic { get; init; } = "SmartEMS/WaterQuality/+/Reading";
}

