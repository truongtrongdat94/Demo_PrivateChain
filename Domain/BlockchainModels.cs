namespace HashAnchorDemo.Domain;

public sealed record ContractDeployment(
    string ContractAddress,
    string TransactionHash,
    long BlockNumber);

public sealed record ChainAnchorEvent(
    string ContractAddress,
    long Sequence,
    string PayloadHash,
    string Submitter,
    DateTimeOffset AnchoredAt,
    string TransactionHash,
    long BlockNumber);
