namespace HashAnchorDemo.Configuration;

public sealed class BatcherConfig
{
    public const string SectionName = "Batcher";

    public int IntervalSeconds { get; init; } = 60;
}
