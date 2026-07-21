using System.Text;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;

namespace HashAnchorDemo;

public sealed class MqttIngestWorker : BackgroundService
{
    private readonly MqttOptions _options;
    private readonly SensorIngestService _ingestService;
    private readonly ILogger<MqttIngestWorker> _logger;

    public MqttIngestWorker(
        IOptions<MqttOptions> options,
        SensorIngestService ingestService,
        ILogger<MqttIngestWorker> logger)
    {
        _options = options.Value;
        _ingestService = ingestService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += HandleMessageAsync;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var clientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(_options.Host, _options.Port)
                    .Build();

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

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        var rawPayload = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.PayloadSegment);

        try
        {
            var stored = await _ingestService.IngestAsync(rawPayload, CancellationToken.None);
            _logger.LogInformation(
                "Stored sensor reading {Id} from {StationId} with hash {PayloadHash}",
                stored.Id,
                stored.StationId,
                stored.PayloadHash);
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

