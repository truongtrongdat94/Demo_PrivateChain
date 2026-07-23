namespace HashAnchorDemo.Domain.Entities;

public sealed class SensorRecord
{
    public long Id { get; set; }

    public string OriginalJson { get; set; } = string.Empty;

    public long? BatchId { get; set; }

    public AnchorBatch? Batch { get; set; }

    public string Status { get; set; } = string.Empty;
}
