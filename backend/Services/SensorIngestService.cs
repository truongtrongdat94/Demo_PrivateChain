using System.Text.Json;
using HashAnchorDemo.Domain;

namespace HashAnchorDemo;

public sealed class SensorIngestService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CanonicalJsonHasher _hasher;
    private readonly RecordRepository _repository;

    public SensorIngestService(CanonicalJsonHasher hasher, RecordRepository repository)
    {
        _hasher = hasher;
        _repository = repository;
    }

    public async Task<StoredRecord> IngestAsync(
        string rawPayload,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(rawPayload);
        var reading = document.RootElement.Deserialize<SensorReadingMessage>(SerializerOptions)
            ?? throw new InvalidOperationException("MQTT payload must be a JSON object.");

        Validate(reading);
        var processed = _hasher.Process(document.RootElement);

        return await _repository.SaveRecordAsync(
            reading.StationId!,
            reading.ObservedAt!.Value,
            processed,
            cancellationToken);
    }

    private static void Validate(SensorReadingMessage reading)
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
}
