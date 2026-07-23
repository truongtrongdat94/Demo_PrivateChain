using HashAnchorDemo.Repositories.Batches;
using HashAnchorDemo.Repositories.Records;
using HashAnchorDemo.Services.Blockchain;
using HashAnchorDemo.Services.Records;

namespace HashAnchorDemo.Services.Batches;

public sealed class BatchAnchorService
{
    private readonly BatchRepository _batchRepository;
    private readonly RecordRepository _recordRepository;
    private readonly MerkleBatchBuilder _merkleBatchBuilder;
    private readonly BesuContractService _contractService;
    private readonly CanonicalJsonHasher _hasher;
    private readonly ILogger<BatchAnchorService> _logger;

    public BatchAnchorService(
        BatchRepository batchRepository,
        RecordRepository recordRepository,
        MerkleBatchBuilder merkleBatchBuilder,
        BesuContractService contractService,
        CanonicalJsonHasher hasher,
        ILogger<BatchAnchorService> logger)
    {
        _batchRepository = batchRepository;
        _recordRepository = recordRepository;
        _merkleBatchBuilder = merkleBatchBuilder;
        _contractService = contractService;
        _hasher = hasher;
        _logger = logger;
    }

    public async Task AnchorPendingRecordsAsync(CancellationToken cancellationToken)
    {
        var preparedBatches = await _batchRepository.ListPreparedBatchesAsync(cancellationToken);
        foreach (var preparedBatch in preparedBatches)
            await AnchorPreparedBatchAsync(preparedBatch, cancellationToken);

        var records = await _recordRepository.ListPendingRecordsAsync(cancellationToken);
        if (records.Count == 0)
        {
            _logger.LogDebug("No pending sensor records to anchor.");
            return;
        }

        var batchId = await _batchRepository.SavePreparedBatchAsync(
            records
                .Select(record => new BatchRepository.BatchRecordData(record.Id, record.OriginalJson))
                .ToArray(),
            cancellationToken);

        await AnchorPreparedBatchAsync(
            new BatchRepository.PreparedBatchRecord(batchId, records.Count),
            cancellationToken);
    }

    private async Task AnchorPreparedBatchAsync(
        BatchRepository.PreparedBatchRecord batch,
        CancellationToken cancellationToken)
    {
        var records = await _batchRepository.ListBatchRecordsAsync(batch.Id, cancellationToken);
        if (records.Count != batch.RecordCount)
        {
            throw new InvalidOperationException(
                $"Batch {batch.Id} contains {batch.RecordCount} records but {records.Count} records were loaded.");
        }

        var merkleRoot = _merkleBatchBuilder.Build(records
            .Select(record => new MerkleBatchBuilder.MerkleRecord(
                record.Id,
                _hasher.HashRawJson(record.OriginalJson)))
            .ToArray());

        var chainAnchor = await _contractService.FindAnchorAsync(
            batch.Id,
            cancellationToken)
            ?? await _contractService.AnchorBatchAsync(
                batch.Id,
                merkleRoot,
                cancellationToken);

        if (!string.Equals(chainAnchor.MerkleRoot, merkleRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The BatchAnchored event root does not match the root sent to the contract.");
        }

        await _batchRepository.MarkBatchAnchoredAsync(
            batch.Id,
            chainAnchor,
            cancellationToken);

        _logger.LogInformation(
            "Anchored batch {BatchId}: {RecordCount} records, transaction {TransactionHash}",
            batch.Id,
            batch.RecordCount,
            chainAnchor.TransactionHash);
    }
}
