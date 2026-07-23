using System.Buffers.Binary;
using System.Security.Cryptography;
namespace HashAnchorDemo.Services.Batches;

public sealed class MerkleBatchBuilder
{
    public sealed record MerkleRecord(long Id, string PayloadHash);

    public string Build(IReadOnlyList<MerkleRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
            throw new ArgumentException("A Merkle tree requires at least one record.", nameof(records));

        var orderedRecords = records.OrderBy(record => record.Id).ToArray();
        if (orderedRecords.Select(record => record.Id).Distinct().Count() != orderedRecords.Length)
            throw new ArgumentException("Record IDs must be unique in a Merkle batch.", nameof(records));

        var levels = new List<List<byte[]>>
        {
            orderedRecords.Select(CreateLeaf).ToList()
        };

        while (levels[^1].Count > 1)
        {
            var currentLevel = levels[^1];
            var parentLevel = new List<byte[]>((currentLevel.Count + 1) / 2);

            for (var index = 0; index < currentLevel.Count; index += 2)
            {
                var left = currentLevel[index];
                var right = index + 1 < currentLevel.Count
                    ? currentLevel[index + 1]
                    : left;
                parentLevel.Add(HashPair(left, right));
            }

            levels.Add(parentLevel);
        }

        return Convert.ToHexString(levels[^1][0]).ToLowerInvariant();
    }

    private static byte[] CreateLeaf(MerkleRecord record)
    {
        var payloadHash = Convert.FromHexString(record.PayloadHash);
        if (payloadHash.Length != 32)
            throw new InvalidOperationException($"Record {record.Id} has an invalid payload hash.");

        Span<byte> input = stackalloc byte[40];
        BinaryPrimitives.WriteInt64BigEndian(input, record.Id);
        payloadHash.CopyTo(input[8..]);
        return SHA256.HashData(input);
    }

    private static byte[] HashPair(byte[] left, byte[] right)
    {
        Span<byte> input = stackalloc byte[64];
        left.CopyTo(input);
        right.CopyTo(input[32..]);
        return SHA256.HashData(input);
    }
}
