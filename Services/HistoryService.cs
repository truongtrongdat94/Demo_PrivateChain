using System.Text.Json;

namespace HashAnchorDemo;

public sealed record HistoryComparison(
    ChainAnchorEvent ChainEvent,
    StoredRecord? DatabaseRecord,
    bool IsSequenceMatch,
    bool IsPayloadHashMatch,
    bool IsPreviousHashMatch,
    bool IsChainLinkValid,
    bool IsDatabaseContentHashValid,
    bool IsMatch);

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

    public async Task<IReadOnlyList<HistoryComparison>> ReadAndCompareAsync(
        CancellationToken cancellationToken)
    {
        var events = (await _contractService.ReadEventsAsync(cancellationToken))
            .OrderBy(item => item.Sequence)
            .ToArray();
        var comparisons = new List<HistoryComparison>();
        long previousSequence = 0;
        var previousPayloadHash = new string('0', 64);

        foreach (var chainEvent in events)
        {
            var databaseRecord = await _repository.FindBySequenceAsync(
                chainEvent.Sequence,
                cancellationToken);
            var isSequenceMatch = databaseRecord?.Sequence == chainEvent.Sequence;
            var isPayloadHashMatch = databaseRecord?.PayloadHash == chainEvent.PayloadHash;
            var isPreviousHashMatch = databaseRecord?.PreviousHash == chainEvent.PreviousHash;
            var isChainLinkValid = chainEvent.Sequence == previousSequence + 1
                && chainEvent.PreviousHash == previousPayloadHash;
            var isDatabaseContentHashValid = IsDatabaseContentHashValid(databaseRecord);

            comparisons.Add(new HistoryComparison(
                chainEvent,
                databaseRecord,
                isSequenceMatch,
                isPayloadHashMatch,
                isPreviousHashMatch,
                isChainLinkValid,
                isDatabaseContentHashValid,
                isSequenceMatch
                    && isPayloadHashMatch
                    && isPreviousHashMatch
                    && isChainLinkValid
                    && isDatabaseContentHashValid));

            previousSequence = chainEvent.Sequence;
            previousPayloadHash = chainEvent.PayloadHash;
        }

        return comparisons.OrderByDescending(item => item.ChainEvent.Sequence).ToArray();
    }

    private bool IsDatabaseContentHashValid(StoredRecord? databaseRecord)
    {
        if (databaseRecord is null)
        {
            return false;
        }

        using var document = JsonDocument.Parse(databaseRecord.OriginalJson);
        var processed = _hasher.Process(document.RootElement);
        return processed.CanonicalJson == databaseRecord.CanonicalJson
            && processed.Sha256 == databaseRecord.PayloadHash;
    }
}
