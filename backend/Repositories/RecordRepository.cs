using Npgsql;
using HashAnchorDemo.Domain;

namespace HashAnchorDemo;

public sealed class RecordRepository
{
    private const string DefaultConnectionString =
        "Host=127.0.0.1;Port=24832;Database=hash_demo;Username=hash_demo;Password=hash_demo";

    private readonly string _connectionString;

    public RecordRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("HashDemo")
            ?? DefaultConnectionString;
    }

    public async Task<StoredRecord> SaveRecordAsync(
        string stationId,
        DateTimeOffset observedAt,
        ProcessedRecord record,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO sensor_records (
                station_id, observed_at, original_json, payload_hash, status)
            VALUES (
                @stationId, @observedAt, @originalJson, @payloadHash, 'pending')
            RETURNING id, station_id, observed_at, original_json, payload_hash, status;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("stationId", stationId);
        command.Parameters.AddWithValue("observedAt", observedAt);
        command.Parameters.AddWithValue("originalJson", record.OriginalJson);
        command.Parameters.AddWithValue("payloadHash", record.PayloadHash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadRecord(reader);
    }

    private static StoredRecord ReadRecord(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetFieldValue<DateTimeOffset>(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5));
}
