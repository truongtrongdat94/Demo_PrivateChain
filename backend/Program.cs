using HashAnchorDemo.Configuration;
using HashAnchorDemo.Data;
using HashAnchorDemo.Repositories.Batches;
using HashAnchorDemo.Repositories.Records;
using HashAnchorDemo.Services.Batches;
using HashAnchorDemo.Services.Blockchain;
using HashAnchorDemo.Services.Records;
using HashAnchorDemo.Workers.Batches;
using HashAnchorDemo.Workers.Ingest;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<CanonicalJsonHasher>();
builder.Services.AddSingleton<MerkleBatchBuilder>();
var connectionString = builder.Configuration.GetConnectionString("HashDemo")
    ?? "Host=127.0.0.1;Port=24832;Database=hash_demo;Username=hash_demo;Password=hash_demo";
builder.Services.AddDbContext<HashDemoDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<RecordRepository>();
builder.Services.AddScoped<BatchRepository>();
builder.Services.AddScoped<SensorIngestService>();
builder.Services.AddSingleton<BesuContractService>();
builder.Services.AddScoped<BatchAnchorService>();
builder.Services.AddScoped<BatchVerificationService>();
builder.Services.AddControllers();
builder.Services.Configure<MqttConfig>(builder.Configuration.GetSection(MqttConfig.SectionName));
builder.Services.Configure<BatcherConfig>(builder.Configuration.GetSection(BatcherConfig.SectionName));

builder.Services.AddHostedService<MqttIngestWorker>();
builder.Services.AddHostedService<BatcherWorker>();

var app = builder.Build();
app.MapControllers();

await app.RunAsync();
