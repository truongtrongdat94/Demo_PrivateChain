namespace HashAnchorDemo.Domain;

public sealed record SensorReadingMessage(
    string? SchemaVersion,
    string? StationId,
    string? StationName,
    DateTimeOffset? ObservedAt,
    WaterQualityReadings? Readings);

public sealed record WaterQualityReadings(
    decimal? Ph,
    decimal? Cod,
    decimal? Bod,
    decimal? Tss,
    decimal? Flow,
    decimal? Temperature);

