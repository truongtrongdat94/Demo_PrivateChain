using HashAnchorDemo.Configuration;
using HashAnchorDemo.Services.Batches;
using Microsoft.Extensions.Options;

namespace HashAnchorDemo.Workers.Batches;

public sealed class BatcherWorker : BackgroundService
{
    private readonly BatcherConfig _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BatcherWorker> _logger;

    public BatcherWorker(
        IOptions<BatcherConfig> options,
        IServiceScopeFactory scopeFactory,
        ILogger<BatcherWorker> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.IntervalSeconds <= 0)
            throw new InvalidOperationException("Batcher:IntervalSeconds must be greater than zero.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));
        _logger.LogInformation("Batcher runs every {IntervalSeconds} seconds.", _options.IntervalSeconds);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var batchAnchorService = scope.ServiceProvider.GetRequiredService<BatchAnchorService>();
                await batchAnchorService.AnchorPendingRecordsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Batch anchoring cycle failed; pending records will be retried next cycle.");
            }
        }
    }
}
