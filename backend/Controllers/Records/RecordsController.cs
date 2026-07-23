using HashAnchorDemo.Repositories.Records;
using Microsoft.AspNetCore.Mvc;

namespace HashAnchorDemo.Controllers.Records;

[ApiController]
[Route("api/records")]
public sealed class RecordsController : ControllerBase
{
    private readonly RecordRepository _repository;

    public RecordsController(RecordRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecords(
        [FromQuery] string? stationId,
        [FromQuery] string? status,
        [FromQuery] int? limit,
        [FromQuery] long? beforeId,
        CancellationToken cancellationToken)
    {
        var normalizedStationId = string.IsNullOrWhiteSpace(stationId) ? null : stationId.Trim();
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        if (normalizedStatus is not null and not ("pending" or "anchored"))
            return BadRequest(new { message = "status must be pending or anchored." });

        var normalizedLimit = limit ?? 200;
        if (normalizedLimit is < 1 or > 500)
            return BadRequest(new { message = "limit must be between 1 and 500." });

        if (beforeId is <= 0)
            return BadRequest(new { message = "beforeId must be greater than 0." });

        var result = await _repository.ListDashboardRecordsAsync(
            normalizedStationId,
            normalizedStatus,
            beforeId,
            normalizedLimit + 1,
            cancellationToken);

        var hasNextPage = result.Count > normalizedLimit;
        var records = result.Take(normalizedLimit).ToArray();
        return Ok(new
        {
            records,
            nextBeforeId = hasNextPage ? (long?)records[^1].Id : null
        });
    }
}
