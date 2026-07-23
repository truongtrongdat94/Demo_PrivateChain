using System.Numerics;
using HashAnchorDemo.Dtos.Blockchain;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace HashAnchorDemo.Services.Blockchain;

public sealed class BesuContractService
{
    private const string DefaultRpcUrl = "http://127.0.0.1:28546";
    private const long DefaultChainId = 1337;
    private static readonly BlockParameter EarliestBlock = BlockParameter.CreateEarliest();
    private static readonly BlockParameter LatestBlock = BlockParameter.CreateLatest();
    private readonly Web3 _web3;
    private readonly string _contractAddress;

    public BesuContractService(IConfiguration configuration)
    {
        _contractAddress = configuration["Blockchain:ContractAddress"]?.ToLowerInvariant()
            ?? throw new InvalidOperationException(
                "Blockchain:ContractAddress is required. Deploy the contract project before starting the backend.");
        var rpcUrl = configuration["Blockchain:RpcUrl"] ?? DefaultRpcUrl;
        var chainId = configuration.GetValue("Blockchain:ChainId", DefaultChainId);
        var privateKey = configuration["Blockchain:AccountPrivateKey"]
            ?? throw new InvalidOperationException(
                "Blockchain:AccountPrivateKey is required to submit batch anchors.");
        _web3 = new Web3(new Account(privateKey, chainId), rpcUrl);
    }

    public async Task<ChainBatchAnchorDto> AnchorBatchAsync(
        long batchId,
        string merkleRoot,
        CancellationToken cancellationToken)
    {
        var handler = _web3.Eth.GetContractTransactionHandler<AnchorBatchFunctionDto>();
        var receipt = await handler.SendRequestAndWaitForReceiptAsync(
            _contractAddress,
            new AnchorBatchFunctionDto
            {
                BatchId = batchId,
                MerkleRoot = Convert.FromHexString(merkleRoot)
            },
            cancellationToken: cancellationToken);

        if (receipt.Status?.Value != 1)
            throw new InvalidOperationException("Besu rejected the batch anchor transaction.");

        var eventHandler = _web3.Eth.GetEvent<BatchAnchoredEventDto>(_contractAddress);
        var eventLog = eventHandler.DecodeAllEventsForEvent(receipt.Logs).SingleOrDefault()
            ?? throw new InvalidOperationException("Anchor transaction did not emit BatchAnchored.");

        if (eventLog.Event.BatchId != batchId)
            throw new InvalidOperationException("BatchAnchored emitted an unexpected batch ID.");

        return new ChainBatchAnchorDto(
            batchId,
            Convert.ToHexString(eventLog.Event.MerkleRoot).ToLowerInvariant(),
            receipt.TransactionHash.ToLowerInvariant());
    }

    // Used by M3 recovery: an already-mined anchor is found from its event so
    // the database can retain the transaction hash too.
    public async Task<ChainBatchAnchorDto?> FindAnchorAsync(
        long batchId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var eventHandler = _web3.Eth.GetEvent<BatchAnchoredEventDto>(_contractAddress);
        var logs = await eventHandler.GetAllChangesAsync(
            eventHandler.CreateFilterInput(EarliestBlock, LatestBlock));
        var matchingLog = logs
            .Where(log => !log.Log.Removed && log.Event.BatchId == batchId)
            .OrderBy(log => log.Log.BlockNumber.Value)
            .FirstOrDefault();

        return matchingLog is null
            ? null
            : new ChainBatchAnchorDto(
                batchId,
                Convert.ToHexString(matchingLog.Event.MerkleRoot).ToLowerInvariant(),
                matchingLog.Log.TransactionHash.ToLowerInvariant());
    }

    public async Task<IReadOnlyList<ChainBatchAnchorDto>> ListAnchorsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var eventHandler = _web3.Eth.GetEvent<BatchAnchoredEventDto>(_contractAddress);
        var logs = await eventHandler.GetAllChangesAsync(
            eventHandler.CreateFilterInput(EarliestBlock, LatestBlock));

        return logs
            .Where(log => !log.Log.Removed)
            .OrderBy(log => log.Log.BlockNumber.Value)
            .ThenBy(log => log.Log.TransactionIndex.Value)
            .ThenBy(log => log.Log.LogIndex.Value)
            .Select(log => new ChainBatchAnchorDto(
                (long)log.Event.BatchId,
                Convert.ToHexString(log.Event.MerkleRoot).ToLowerInvariant(),
                log.Log.TransactionHash.ToLowerInvariant()))
            .ToArray();
    }

}
