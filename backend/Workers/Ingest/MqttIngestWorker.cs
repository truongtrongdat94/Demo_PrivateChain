using System.Text;
using HashAnchorDemo.Configuration;
using HashAnchorDemo.Services.Records;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;

namespace HashAnchorDemo.Workers.Ingest;

public sealed class MqttIngestWorker : BackgroundService
{
    private readonly MqttConfig _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MqttIngestWorker> _logger;

    public MqttIngestWorker(
        IOptions<MqttConfig> options,
        IServiceScopeFactory scopeFactory,
        ILogger<MqttIngestWorker> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += eventArgs =>
            HandleMessageAsync(eventArgs, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var clientOptionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_options.Host, _options.Port);

                if (!string.IsNullOrWhiteSpace(_options.Username))
                    clientOptionsBuilder.WithCredentials(_options.Username, _options.Password);

                var clientOptions = clientOptionsBuilder.Build();

                await client.ConnectAsync(clientOptions, stoppingToken);
                await client.SubscribeAsync(
                    new MqttTopicFilterBuilder().WithTopic(_options.Topic).Build(),
                    stoppingToken);

                _logger.LogInformation(
                    "Subscribed to MQTT topic {Topic} at {Host}:{Port}",
                    _options.Topic,
                    _options.Host,
                    _options.Port);

                while (client.IsConnected && !stoppingToken.IsCancellationRequested)
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Cannot connect to MQTT broker {Host}:{Port}; retrying in 5 seconds",
                    _options.Host,
                    _options.Port);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(
        MqttApplicationMessageReceivedEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        var rawPayload = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.PayloadSegment);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ingestService = scope.ServiceProvider.GetRequiredService<SensorIngestService>();
            var recordId = await ingestService.IngestAsync(rawPayload, cancellationToken);
            _logger.LogInformation(
                "Stored sensor reading {Id}",
                recordId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Rejected MQTT message from topic {Topic}",
                eventArgs.ApplicationMessage.Topic);
        }
    }
}
