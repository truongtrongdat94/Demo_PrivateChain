using HashAnchorDemo.Repositories.Records;
using Microsoft.AspNetCore.Mvc;

namespace HashAnchorDemo.Controllers.Stations;

[ApiController]
[Route("api/stations")]
public sealed class StationsController : ControllerBase
{
    private readonly RecordRepository _repository;

    public StationsController(RecordRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetStations(CancellationToken cancellationToken)
    {
        var result = await _repository.ListStationIdsAsync(cancellationToken);
        return Ok(result);
    }
}
