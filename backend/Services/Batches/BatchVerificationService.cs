using HashAnchorDemo.Repositories.Batches;
using HashAnchorDemo.Dtos.Blockchain;
using HashAnchorDemo.Services.Blockchain;
using HashAnchorDemo.Services.Records;

namespace HashAnchorDemo.Services.Batches;

public sealed class BatchVerificationService
{
    private readonly BatchRepository _repository;
    private readonly CanonicalJsonHasher _hasher;
    private readonly MerkleBatchBuilder _merkleBatchBuilder;
    private readonly BesuContractService _contractService;

    public BatchVerificationService(
        BatchRepository repository,
        CanonicalJsonHasher hasher,
        MerkleBatchBuilder merkleBatchBuilder,
        BesuContractService contractService)
    {
        _repository = repository;
        _hasher = hasher;
        _merkleBatchBuilder = merkleBatchBuilder;
        _contractService = contractService;
    }

    public async Task<IReadOnlyDictionary<long, string>> VerifyAllFromChainAsync(CancellationToken cancellationToken)
    {
        var anchors = await _contractService.ListAnchorsAsync(cancellationToken);
        var errors = new Dictionary<long, string>();

        foreach (var anchor in anchors)
        {
            var error = await GetVerificationErrorAsync(anchor, cancellationToken);
            if (error is not null)
                errors[anchor.BatchId] = error;
        }

        return errors;
    }

    private async Task<string?> GetVerificationErrorAsync(
        ChainBatchAnchorDto chainAnchor,
        CancellationToken cancellationToken)
    {
        var batch = await _repository.FindAnchoredBatchAsync(chainAnchor.BatchId, cancellationToken);
        if (batch is null)
            return $"Batch {chainAnchor.BatchId}: database empty or batch not found.";

        var records = await _repository.ListBatchRecordsAsync(chainAnchor.BatchId, cancellationToken);
        var rebuiltRoot = TryRebuildMerkleRoot(records);
        var isValid = records.Count == batch.RecordCount
            && string.Equals(rebuiltRoot, chainAnchor.MerkleRoot, StringComparison.OrdinalIgnoreCase);

        return isValid
            ? null
            : $"Batch {chainAnchor.BatchId}: database does not match blockchain.";
    }

    private string? TryRebuildMerkleRoot(IReadOnlyList<BatchRepository.BatchRecordData> records)
    {
        if (records.Count == 0)
            return null;

        try
        {
            return _merkleBatchBuilder.Build(records
                .Select(record => new MerkleBatchBuilder.MerkleRecord(
                    record.Id,
                    _hasher.HashRawJson(record.OriginalJson)))
                .ToArray());
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
