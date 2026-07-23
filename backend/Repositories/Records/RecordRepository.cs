using System.Text.Json;
using HashAnchorDemo.Data;
using HashAnchorDemo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HashAnchorDemo.Repositories.Records;

public sealed class RecordRepository
{
    public sealed record PendingRecordData(long Id, string OriginalJson);

    public sealed record DashboardRecord(
        long Id,
        string? StationId,
        DateTimeOffset? ObservedAt,
        DashboardReadings? Readings,
        string Status,
        long? BatchId);

    public sealed record DashboardReadings(
        decimal? Ph,
        decimal? Cod,
        decimal? Bod,
        decimal? Tss,
        decimal? Flow,
        decimal? Temperature);

    private readonly HashDemoDbContext _db;

    public RecordRepository(HashDemoDbContext db)
    {
        _db = db;
    }

    public async Task<long> SaveRecordAsync(string originalJson, CancellationToken cancellationToken)
    {
        var entity = new SensorRecord
        {
            OriginalJson = originalJson,
            Status = "pending"
        };

        _db.SensorRecords.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<IReadOnlyList<PendingRecordData>> ListPendingRecordsAsync(CancellationToken cancellationToken) =>
        await _db.SensorRecords
            .AsNoTracking()
            .Where(record => record.Status == "pending" && record.BatchId == null)
            .OrderBy(record => record.Id)
            .Select(record => new PendingRecordData(record.Id, record.OriginalJson))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListStationIdsAsync(CancellationToken cancellationToken)
    {
        var payloads = await _db.SensorRecords
            .AsNoTracking()
            .OrderBy(record => record.Id)
            .Select(record => record.OriginalJson)
            .ToArrayAsync(cancellationToken);

        return payloads
            .Select(ReadDashboardPayload)
            .Select(payload => payload.StationId)
            .Where(stationId => !string.IsNullOrWhiteSpace(stationId))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(stationId => stationId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<DashboardRecord>> ListDashboardRecordsAsync(
        string? stationId,
        string? status,
        long? beforeId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = _db.SensorRecords.AsNoTracking().AsQueryable();
        if (status is not null)
            query = query.Where(record => record.Status == status);

        if (beforeId is not null)
            query = query.Where(record => record.Id < beforeId.Value);

        var storedRecords = await query
            .OrderByDescending(record => record.Id)
            .Select(record => new { record.Id, record.OriginalJson, record.Status, record.BatchId })
            .ToArrayAsync(cancellationToken);

        return storedRecords
            .Select(record => new { Record = record, Payload = ReadDashboardPayload(record.OriginalJson) })
            .Where(item => stationId is null || string.Equals(item.Payload.StationId, stationId, StringComparison.Ordinal))
            .Take(limit)
            .Select(item => new DashboardRecord(
                item.Record.Id,
                item.Payload.StationId,
                item.Payload.ObservedAt,
                item.Payload.Readings,
                item.Record.Status,
                item.Record.BatchId))
            .ToArray();
    }

    private static DashboardPayload ReadDashboardPayload(string originalJson)
    {
        try
        {
            using var document = JsonDocument.Parse(originalJson);
            var payload = document.RootElement;
            if (payload.ValueKind != JsonValueKind.Object)
                return new DashboardPayload(null, null, null);

            var stationId = ReadString(payload, "stationId");
            var observedAtText = ReadString(payload, "observedAt");
            DateTimeOffset? observedAt = DateTimeOffset.TryParse(observedAtText, out var parsed) ? parsed : null;
            if (!payload.TryGetProperty("readings", out var readings) || readings.ValueKind != JsonValueKind.Object)
                return new DashboardPayload(stationId, observedAt, null);

            return new DashboardPayload(stationId, observedAt, new DashboardReadings(
                ReadDecimal(readings, "ph"), ReadDecimal(readings, "cod"), ReadDecimal(readings, "bod"),
                ReadDecimal(readings, "tss"), ReadDecimal(readings, "flow"), ReadDecimal(readings, "temperature")));
        }
        catch (JsonException)
        {
            return new DashboardPayload(null, null, null);
        }
    }

    private static string? ReadString(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static decimal? ReadDecimal(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var result) ? result : null;

    private sealed record DashboardPayload(string? StationId, DateTimeOffset? ObservedAt, DashboardReadings? Readings);
}
