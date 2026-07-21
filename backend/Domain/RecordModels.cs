namespace HashAnchorDemo.Domain;

public sealed record ProcessedRecord(
    string OriginalJson,
    string PayloadHash);

public sealed record StoredRecord(
    long Id,
    string StationId,
    DateTimeOffset ObservedAt,
    string OriginalJson,
    string PayloadHash,
    string Status);
