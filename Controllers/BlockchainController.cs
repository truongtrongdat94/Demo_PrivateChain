using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace HashAnchorDemo.Controllers;

[ApiController]
[Route("api/blockchain")]
public sealed class BlockchainController : ControllerBase
{
    private readonly CanonicalJsonHasher _hasher;
    private readonly RecordRepository _repository;
    private readonly BesuContractService _contractService;
    private readonly HistoryService _historyService;

    public BlockchainController(
        CanonicalJsonHasher hasher,
        RecordRepository repository,
        BesuContractService contractService,
        HistoryService historyService)
    {
        _hasher = hasher;
        _repository = repository;
        _contractService = contractService;
        _historyService = historyService;
    }

    [HttpPost("anchor")]
    public async Task<ActionResult> Anchor(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return BadRequest("Body phải là một JSON object: { ... }");
        }

        var processed = _hasher.Process(payload);
        var anchor = await _contractService.AnchorHashAsync(processed.Sha256, cancellationToken);
        var stored = await _repository.SaveRecordAsync(
            processed,
            anchor.Sequence,
            anchor.PreviousHash,
            cancellationToken);

        return Ok(new { database = stored, blockchain = anchor });
    }

    [HttpGet("history")]
    public async Task<ActionResult> History(CancellationToken cancellationToken)
    {
        var history = await _historyService.ReadAndCompareAsync(cancellationToken);
        return Ok(history);
    }

    [HttpGet("records")]
    public async Task<ActionResult> Records(CancellationToken cancellationToken)
    {
        var records = await _repository.ListRecordsAsync(cancellationToken);
        return Ok(records);
    }
}
