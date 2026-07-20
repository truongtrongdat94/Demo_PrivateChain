namespace HashAnchorDemo.Domain;

public sealed record HistoryComparison(
    ChainAnchorEvent ChainEvent,
    StoredRecord? DatabaseRecord,
    bool IsMatch);

public sealed record HistoryAudit(
    bool IsValid,
    IReadOnlyList<HistoryComparison> Records);
