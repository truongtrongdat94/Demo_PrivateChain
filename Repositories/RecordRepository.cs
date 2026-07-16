using Npgsql;

namespace HashAnchorDemo;

public sealed record StoredRecord(
    Guid Id,
    long Sequence,
    string OriginalJson,
    string CanonicalJson,
    string PayloadHash,
    string PreviousHash,
    DateTimeOffset CreatedAt);

public sealed class RecordRepository
{
    // Local PostgreSQL container only. Hardcoded intentionally for this demo.
    private const string ConnectionString =
        "Host=127.0.0.1;Port=25432;Database=hash_demo;Username=hash_demo;Password=hash_demo";

    public async Task<StoredRecord> SaveRecordAsync(
        ProcessedRecord record,
        long sequence,
        string previousHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO records (
                id, sequence, original_json, canonical_json, payload_hash, previous_hash)
            VALUES (
                @id, @sequence, @originalJson, @canonicalJson, @payloadHash, @previousHash)
            RETURNING id, sequence, original_json, canonical_json,
                      payload_hash, previous_hash, created_at;
            """;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("sequence", sequence);
        command.Parameters.AddWithValue("originalJson", record.OriginalJson);
        command.Parameters.AddWithValue("canonicalJson", record.CanonicalJson);
        command.Parameters.AddWithValue("payloadHash", record.Sha256);
        command.Parameters.AddWithValue("previousHash", previousHash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadRecord(reader);
    }

    public async Task<StoredRecord?> FindBySequenceAsync(
        long sequence,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, sequence, original_json, canonical_json,
                   payload_hash, previous_hash, created_at
            FROM records
            WHERE sequence = @sequence;
            """;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("sequence", sequence);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRecord(reader) : null;
    }

    public async Task<IReadOnlyList<StoredRecord>> ListRecordsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, sequence, original_json, canonical_json,
                   payload_hash, previous_hash, created_at
            FROM records
            ORDER BY sequence DESC;
            """;

        var records = new List<StoredRecord>();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadRecord(reader));
        }
        return records;
    }

    public async Task<string?> GetContractAddressAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT value FROM runtime_settings WHERE key = 'contract_address';";
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task SaveContractAddressAsync(string address, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO runtime_settings (key, value)
            VALUES ('contract_address', @address)
            ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value;
            """;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("address", address);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static StoredRecord ReadRecord(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetInt64(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetFieldValue<DateTimeOffset>(6));
}
