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
        ProcessedRecord record,
        HashAnchor anchor,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO records (
                contract_address, sequence, original_json)
            VALUES (
                @contractAddress, @sequence, @originalJson)
            RETURNING contract_address, sequence, original_json;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("contractAddress", anchor.ContractAddress);
        command.Parameters.AddWithValue("sequence", anchor.Sequence);
        command.Parameters.AddWithValue("originalJson", record.OriginalJson);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadRecord(reader);
    }

    public async Task<IReadOnlyList<StoredRecord>> ListRecordsByContractAddressAsync(
        string contractAddress,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT contract_address, sequence, original_json
            FROM records
            WHERE contract_address = @contractAddress
            ORDER BY sequence;
            """;

        var records = new List<StoredRecord>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("contractAddress", contractAddress);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadRecord(reader));
        }
        return records;
    }

    public async Task<IReadOnlyList<StoredRecord>> ListRecordsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT contract_address, sequence, original_json
            FROM records
            ORDER BY sequence DESC;
            """;

        var records = new List<StoredRecord>();
        await using var connection = new NpgsqlConnection(_connectionString);
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
        await using var connection = new NpgsqlConnection(_connectionString);
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

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("address", address.ToLowerInvariant());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static StoredRecord ReadRecord(NpgsqlDataReader reader) => new(
        reader.GetString(0),
        reader.GetInt64(1),
        reader.GetString(2));
}
