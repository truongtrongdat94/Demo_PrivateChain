using System.Text.Json;
using HashAnchorDemo.Repositories.Records;

namespace HashAnchorDemo.Services.Records;

public sealed class SensorIngestService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly RecordRepository _repository;

    public SensorIngestService(RecordRepository repository)
    {
        _repository = repository;
    }

    public async Task<long> IngestAsync(
        string rawPayload,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(rawPayload);
        var reading = document.RootElement.Deserialize<SensorReadingMessageDto>(SerializerOptions)
            ?? throw new InvalidOperationException("MQTT payload must be a JSON object.");

        Validate(reading);
        return await _repository.SaveRecordAsync(
            document.RootElement.GetRawText(),
            cancellationToken);
    }

    private static void Validate(SensorReadingMessageDto reading)
    {
        if (reading.SchemaVersion != "1.0")
            throw new InvalidOperationException("schemaVersion must be 1.0.");

        if (string.IsNullOrWhiteSpace(reading.StationId))
            throw new InvalidOperationException("stationId is required.");

        if (string.IsNullOrWhiteSpace(reading.StationName))
            throw new InvalidOperationException("stationName is required.");

        if (reading.ObservedAt is null)
            throw new InvalidOperationException("observedAt is required.");

        var values = reading.Readings
            ?? throw new InvalidOperationException("readings is required.");

        if (values.Ph is null or < 0 or > 14)
            throw new InvalidOperationException("readings.ph must be between 0 and 14.");

        if (values.Cod is null or < 0 ||
            values.Bod is null or < 0 ||
            values.Tss is null or < 0 ||
            values.Flow is null or < 0 ||
            values.Temperature is null)
        {
            throw new InvalidOperationException("All readings are required; COD, BOD, TSS and flow must be non-negative.");
        }
    }

    private sealed record SensorReadingMessageDto(
        string? SchemaVersion,
        string? StationId,
        string? StationName,
        DateTimeOffset? ObservedAt,
        WaterQualityReadingsDto? Readings);

    private sealed record WaterQualityReadingsDto(
        decimal? Ph,
        decimal? Cod,
        decimal? Bod,
        decimal? Tss,
        decimal? Flow,
        decimal? Temperature);
}
