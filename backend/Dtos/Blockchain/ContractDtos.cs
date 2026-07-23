using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace HashAnchorDemo.Dtos.Blockchain;

[Function("anchorBatch")]
public sealed class AnchorBatchFunctionDto : FunctionMessage
{
    [Parameter("uint256", "batchId", 1)]
    public BigInteger BatchId { get; init; }

    [Parameter("bytes32", "merkleRoot", 2)]
    public byte[] MerkleRoot { get; init; } = Array.Empty<byte>();
}

[Event("BatchAnchored")]
public sealed class BatchAnchoredEventDto : IEventDTO
{
    [Parameter("uint256", "batchId", 1, true)]
    public BigInteger BatchId { get; set; }

    [Parameter("bytes32", "merkleRoot", 2, true)]
    public byte[] MerkleRoot { get; set; } = Array.Empty<byte>();

    [Parameter("address", "submitter", 3, true)]
    public string Submitter { get; set; } = string.Empty;

    [Parameter("uint64", "anchoredAt", 4, false)]
    public ulong AnchoredAt { get; set; }
}
