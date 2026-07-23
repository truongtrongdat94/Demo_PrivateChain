using HashAnchorDemo.Services.Batches;
using Microsoft.AspNetCore.Mvc;

namespace HashAnchorDemo.Controllers.Batches;

[ApiController]
[Route("api/batches")]
public sealed class BatchesController : ControllerBase
{
    private readonly BatchVerificationService _verificationService;

    public BatchesController(BatchVerificationService verificationService)
    {
        _verificationService = verificationService;
    }

    [HttpGet("verify-all")]
    public async Task<IActionResult> VerifyAll(CancellationToken cancellationToken)
    {
        var result = await _verificationService.VerifyAllFromChainAsync(cancellationToken);
        return Ok(result);
    }
}
