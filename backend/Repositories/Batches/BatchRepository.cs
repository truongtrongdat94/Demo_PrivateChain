using HashAnchorDemo.Data;
using HashAnchorDemo.Domain.Entities;
using HashAnchorDemo.Dtos.Blockchain;
using Microsoft.EntityFrameworkCore;

namespace HashAnchorDemo.Repositories.Batches;

public sealed class BatchRepository
{
    public sealed record AnchoredBatchRecord(long Id, int RecordCount, string TransactionHash);
    public sealed record PreparedBatchRecord(long Id, int RecordCount);
    public sealed record BatchRecordData(long Id, string OriginalJson);

    private readonly HashDemoDbContext _db;

    public BatchRepository(HashDemoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PreparedBatchRecord>> ListPreparedBatchesAsync(CancellationToken cancellationToken) =>
        await _db.AnchorBatches
            .AsNoTracking()
            .Where(batch => batch.Status == "prepared")
            .OrderBy(batch => batch.Id)
            .Select(batch => new PreparedBatchRecord(batch.Id, batch.RecordCount))
            .ToArrayAsync(cancellationToken);

    public async Task<long> SavePreparedBatchAsync(
        IReadOnlyList<BatchRecordData> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
            throw new ArgumentException("A prepared batch requires at least one record.", nameof(records));

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var batch = new AnchorBatch { RecordCount = records.Count, Status = "prepared" };
        _db.AnchorBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var record in records)
        {
            var updated = await _db.SensorRecords
                .Where(entity => entity.Id == record.Id &&
                    entity.OriginalJson == record.OriginalJson &&
                    entity.Status == "pending" &&
                    entity.BatchId == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.BatchId, batch.Id), cancellationToken);

            if (updated != 1)
                throw new InvalidOperationException($"Pending record {record.Id} was claimed or changed by another batcher.");
        }

        await transaction.CommitAsync(cancellationToken);
        return batch.Id;
    }

    public async Task MarkBatchAnchoredAsync(long batchId, ChainBatchAnchorDto anchor, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await _db.AnchorBatches.SingleOrDefaultAsync(
            entity => entity.Id == batchId && entity.Status == "prepared",
            cancellationToken)
            ?? throw new InvalidOperationException($"Prepared batch {batchId} cannot be finalized.");

        var updatedRecords = await _db.SensorRecords
            .Where(record => record.BatchId == batchId && record.Status == "pending")
            .ExecuteUpdateAsync(setters => setters.SetProperty(record => record.Status, "anchored"), cancellationToken);

        if (updatedRecords != batch.RecordCount)
            throw new InvalidOperationException($"Batch {batchId} contains {batch.RecordCount} records but {updatedRecords} records were finalized.");

        batch.TransactionHash = anchor.TransactionHash;
        batch.Status = "anchored";
        batch.AnchoredAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AnchoredBatchRecord?> FindAnchoredBatchAsync(
        long batchId,
        CancellationToken cancellationToken)
    {
        var batch = await _db.AnchorBatches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                batch => batch.Id == batchId && batch.Status == "anchored",
                cancellationToken);

        return batch is null
            ? null
            : new AnchoredBatchRecord(batch.Id, batch.RecordCount, batch.TransactionHash!);
    }

    public async Task<IReadOnlyList<BatchRecordData>> ListBatchRecordsAsync(
        long batchId,
        CancellationToken cancellationToken) =>
        await _db.SensorRecords
            .AsNoTracking()
            .Where(record => record.BatchId == batchId)
            .OrderBy(record => record.Id)
            .Select(record => new BatchRecordData(record.Id, record.OriginalJson))
            .ToArrayAsync(cancellationToken);
}
