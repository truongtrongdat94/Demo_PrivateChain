namespace HashAnchorDemo.Dtos.Blockchain;

public sealed record ChainBatchAnchorDto(
    long BatchId,
    string MerkleRoot,
    string TransactionHash);
