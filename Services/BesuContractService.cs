using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace HashAnchorDemo;

public sealed record ContractDeployment(string ContractAddress, string TransactionHash, long BlockNumber);
public sealed record HashAnchor(
    string ContractAddress,
    string TransactionHash,
    long BlockNumber,
    long Sequence,
    string PayloadHash,
    string PreviousHash);
public sealed record ChainAnchorEvent(
    long Sequence,
    string PayloadHash,
    string PreviousHash,
    string Submitter,
    DateTimeOffset AnchoredAt,
    string TransactionHash,
    long BlockNumber);

public sealed class BesuContractService
{
    private const string DefaultBesuRpcUrl = "http://127.0.0.1:28546";
    private const long DefaultChainId = 1337;
    private const string DefaultAccountPrivateKey =
        "0x8f2a55949038a9610f50fb23b5883af3b4ecb3c3bb792cbcefbd1542c692be63";

    private readonly RecordRepository _repository;
    private readonly Web3 _web3;
    private readonly SemaphoreSlim _anchorLock = new(1, 1);

    public BesuContractService(RecordRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        var besuRpcUrl = configuration["Besu:RpcUrl"] ?? DefaultBesuRpcUrl;
        var chainId = configuration.GetValue("Besu:ChainId", DefaultChainId);
        var accountPrivateKey = configuration["Besu:AccountPrivateKey"] ?? DefaultAccountPrivateKey;
        var account = new Account(accountPrivateKey, chainId);
        _web3 = new Web3(account, besuRpcUrl);
    }

    public async Task<ContractDeployment> DeployAsync(CancellationToken cancellationToken)
    {
        var handler = _web3.Eth.GetContractDeploymentHandler<HashRegistryDeployment>();
        var receipt = await handler.SendRequestAndWaitForReceiptAsync(
            new HashRegistryDeployment(),
            cancellationToken: cancellationToken);
        var contractAddress = receipt.ContractAddress
            ?? throw new InvalidOperationException("Besu did not return a contract address");

        await _repository.SaveContractAddressAsync(contractAddress, cancellationToken);
        return new ContractDeployment(
            contractAddress,
            receipt.TransactionHash,
            (long)receipt.BlockNumber.Value);
    }

    public async Task<ContractDeployment?> DeployIfMissingAsync(CancellationToken cancellationToken)
    {
        var currentAddress = await _repository.GetContractAddressAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(currentAddress)
            ? await DeployAsync(cancellationToken)
            : null;
    }

    public async Task<HashAnchor> AnchorHashAsync(
        string payloadHash,
        CancellationToken cancellationToken)
    {
        var contractAddress = await _repository.GetContractAddressAsync(cancellationToken)
            ?? throw new InvalidOperationException("Deploy the contract first with the deploy script");

        await _anchorLock.WaitAsync(cancellationToken);
        try
        {
            var sequenceHandler = _web3.Eth.GetContractQueryHandler<LastSequenceFunction>();
            var payloadHashHandler = _web3.Eth.GetContractQueryHandler<LastPayloadHashFunction>();
            var lastSequence = await sequenceHandler.QueryAsync<System.Numerics.BigInteger>(
                contractAddress,
                new LastSequenceFunction());
            var previousHashBytes = await payloadHashHandler.QueryAsync<byte[]>(
                contractAddress,
                new LastPayloadHashFunction());
            var sequence = checked((long)lastSequence + 1);
            var previousHash = Convert.ToHexString(previousHashBytes).ToLowerInvariant();

            var handler = _web3.Eth.GetContractTransactionHandler<AnchorHashFunction>();
            var receipt = await handler.SendRequestAndWaitForReceiptAsync(
                contractAddress,
                new AnchorHashFunction
                {
                    Sequence = sequence,
                    PayloadHash = Convert.FromHexString(payloadHash),
                    PreviousHash = previousHashBytes
                },
                cancellationToken: cancellationToken);

            return new HashAnchor(
                contractAddress,
                receipt.TransactionHash,
                (long)receipt.BlockNumber.Value,
                sequence,
                payloadHash,
                previousHash);
        }
        finally
        {
            _anchorLock.Release();
        }
    }

    public async Task<IReadOnlyList<ChainAnchorEvent>> ReadEventsAsync(CancellationToken cancellationToken)
    {
        var contractAddress = await _repository.GetContractAddressAsync(cancellationToken)
            ?? throw new InvalidOperationException("Deploy the contract first with the deploy script");
        var eventHandler = _web3.Eth.GetEvent<HashAnchoredEventDto>(contractAddress);
        var logs = await eventHandler.GetAllChangesAsync(eventHandler.CreateFilterInput());

        return logs
            .Select(log => new ChainAnchorEvent(
                (long)log.Event.Sequence,
                Convert.ToHexString(log.Event.PayloadHash).ToLowerInvariant(),
                Convert.ToHexString(log.Event.PreviousHash).ToLowerInvariant(),
                log.Event.Submitter,
                DateTimeOffset.FromUnixTimeSeconds((long)log.Event.AnchoredAt),
                log.Log.TransactionHash,
                (long)log.Log.BlockNumber.Value))
            .OrderByDescending(item => item.Sequence)
            .ToArray();
    }
}
