using HashAnchorDemo;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<CanonicalJsonHasher>();
builder.Services.AddSingleton<RecordRepository>();
builder.Services.AddSingleton<SensorIngestService>();
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection(MqttOptions.SectionName));
builder.Services.AddHostedService<MqttIngestWorker>();

var host = builder.Build();
await host.RunAsync();
