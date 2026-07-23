namespace HashAnchorDemo.Domain.Entities;

public sealed class AnchorBatch
{
    public long Id { get; set; }

    public int RecordCount { get; set; }

    public string? TransactionHash { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? AnchoredAt { get; set; }

    public ICollection<SensorRecord> Records { get; } = new List<SensorRecord>();
}
