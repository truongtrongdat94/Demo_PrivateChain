namespace HashAnchorDemo.Domain;

public sealed record ProcessedRecord(
    string OriginalJson,
    string PayloadHash);

public sealed record StoredRecord(
    string ContractAddress,
    long Sequence,
    string OriginalJson);

public sealed record HashAnchor(
    string ContractAddress,
    string TransactionHash,
    long BlockNumber,
    long Sequence,
    string PayloadHash,
    string Submitter,
    DateTimeOffset AnchoredAt);

public sealed record AnchoredRecord(
    ProcessedRecord Record,
    HashAnchor Anchor);
