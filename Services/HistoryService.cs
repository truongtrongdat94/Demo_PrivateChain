using System.Text.Json;
using HashAnchorDemo.Domain;

namespace HashAnchorDemo;

public sealed class HistoryService
{
    private readonly BesuContractService _contractService;
    private readonly RecordRepository _repository;
    private readonly CanonicalJsonHasher _hasher;

    public HistoryService(
        BesuContractService contractService,
        RecordRepository repository,
        CanonicalJsonHasher hasher)
    {
        _contractService = contractService;
        _repository = repository;
        _hasher = hasher;
    }

    public async Task<HistoryAudit> ReadAndCompareAsync(
        CancellationToken cancellationToken)
    {
        var contractAddress = await _contractService.GetContractAddressAsync(cancellationToken);
        var events = (await _contractService.ReadEventsAsync(cancellationToken))
            .OrderBy(item => item.Sequence)
            .ToArray();
        var databaseRecords = await _repository.ListRecordsByContractAddressAsync(
            contractAddress,
            cancellationToken);
        var recordsBySequence = databaseRecords
            .GroupBy(record => record.Sequence)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var comparisons = new List<HistoryComparison>();
        foreach (var chainEvent in events)
        {
            recordsBySequence.TryGetValue(chainEvent.Sequence, out var matchedRecords);
            var databaseRecord = matchedRecords?.FirstOrDefault();
            var isMatch = databaseRecord is not null
                && matchedRecords?.Length == 1
                && IsDatabasePayloadHashMatch(databaseRecord, chainEvent);

            comparisons.Add(new HistoryComparison(
                chainEvent,
                databaseRecord,
                isMatch));
        }

        var orderedComparisons = comparisons
            .OrderByDescending(item => item.ChainEvent.Sequence)
            .ToArray();
        return new HistoryAudit(
            databaseRecords.Count == events.Length
                && orderedComparisons.All(comparison => comparison.IsMatch),
            orderedComparisons);
    }

    private bool IsDatabasePayloadHashMatch(
        StoredRecord? databaseRecord,
        ChainAnchorEvent chainEvent)
    {
        if (databaseRecord is null)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(databaseRecord.OriginalJson);
            var processed = _hasher.Process(document.RootElement);
            return EqualsIgnoreCase(processed.PayloadHash, chainEvent.PayloadHash);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool EqualsIgnoreCase(string? first, string? second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
