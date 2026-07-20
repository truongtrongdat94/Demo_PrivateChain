using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using HashAnchorDemo.Domain;
using System.Text.Json;

namespace HashAnchorDemo;

public sealed class BesuContractService
{
    private const string DefaultBesuRpcUrl = "http://127.0.0.1:28546";
    private const long DefaultChainId = 1337;
    private const string DefaultAccountPrivateKey =
        "0x8f2a55949038a9610f50fb23b5883af3b4ecb3c3bb792cbcefbd1542c692be63";

    private readonly RecordRepository _repository;
    private readonly CanonicalJsonHasher _hasher;
    private readonly Web3 _web3;
    private readonly string? _configuredContractAddress;

    public BesuContractService(
        RecordRepository repository,
        CanonicalJsonHasher hasher,
        IConfiguration configuration)
    {
        _repository = repository;
        _hasher = hasher;
        _configuredContractAddress = configuration["Besu:ContractAddress"]?.ToLowerInvariant();
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
        if (!string.IsNullOrWhiteSpace(_configuredContractAddress))
        {
            return null;
        }

        var currentAddress = await _repository.GetContractAddressAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(currentAddress)
            ? await DeployAsync(cancellationToken)
            : null;
    }

    public async Task<AnchoredRecord> AnchorPayloadAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var contractAddress = await GetContractAddressAsync(cancellationToken);
        var processed = _hasher.Process(payload);
        var handler = _web3.Eth.GetContractTransactionHandler<AnchorHashFunction>();
        var receipt = await handler.SendRequestAndWaitForReceiptAsync(
            contractAddress,
            new AnchorHashFunction
            {
                PayloadHash = Convert.FromHexString(processed.PayloadHash)
            },
            cancellationToken: cancellationToken);

        var eventHandler = _web3.Eth.GetEvent<HashAnchoredEventDto>(contractAddress);
        var eventLog = eventHandler.DecodeAllEventsForEvent(receipt.Logs).SingleOrDefault()
            ?? throw new InvalidOperationException("Anchor transaction did not emit HashAnchored");
        var chainEvent = ToChainAnchorEvent(eventLog.Log, eventLog.Event);

        return new AnchoredRecord(
            processed,
            new HashAnchor(
                chainEvent.ContractAddress,
                chainEvent.TransactionHash,
                chainEvent.BlockNumber,
                chainEvent.Sequence,
                chainEvent.PayloadHash,
                chainEvent.Submitter,
                chainEvent.AnchoredAt));
    }

    public async Task<string> GetContractAddressAsync(CancellationToken cancellationToken) =>
        _configuredContractAddress
        ?? (await _repository.GetContractAddressAsync(cancellationToken))?.ToLowerInvariant()
        ?? throw new InvalidOperationException("Deploy the contract first with the deploy script");

    public async Task<IReadOnlyList<ChainAnchorEvent>> ReadEventsAsync(CancellationToken cancellationToken)
    {
        var contractAddress = await GetContractAddressAsync(cancellationToken);
        var eventHandler = _web3.Eth.GetEvent<HashAnchoredEventDto>(contractAddress);
        var logs = await eventHandler.GetAllChangesAsync(eventHandler.CreateFilterInput());

        return logs
            .Select(log => ToChainAnchorEvent(log.Log, log.Event))
            .OrderByDescending(item => item.Sequence)
            .ToArray();
    }

    private static ChainAnchorEvent ToChainAnchorEvent(
        Nethereum.RPC.Eth.DTOs.FilterLog log,
        HashAnchoredEventDto anchoredEvent) => new(
        log.Address.ToLowerInvariant(),
        (long)anchoredEvent.Sequence,
        Convert.ToHexString(anchoredEvent.PayloadHash).ToLowerInvariant(),
        anchoredEvent.Submitter.ToLowerInvariant(),
        DateTimeOffset.FromUnixTimeSeconds((long)anchoredEvent.AnchoredAt),
        log.TransactionHash.ToLowerInvariant(),
        (long)log.BlockNumber.Value);
}
