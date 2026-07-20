using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace HashAnchorDemo.Controllers;

[ApiController]
[Route("api/blockchain")]
public sealed class BlockchainController : ControllerBase
{
    private readonly RecordRepository _repository;
    private readonly BesuContractService _contractService;
    private readonly HistoryService _historyService;

    public BlockchainController(
        RecordRepository repository,
        BesuContractService contractService,
        HistoryService historyService)
    {
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

        var anchored = await _contractService.AnchorPayloadAsync(payload, cancellationToken);
        var stored = await _repository.SaveRecordAsync(
            anchored.Record,
            anchored.Anchor,
            cancellationToken);

        return Ok(new { database = stored, blockchain = anchored.Anchor });
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
