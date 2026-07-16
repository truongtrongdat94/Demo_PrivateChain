using HashAnchorDemo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<CanonicalJsonHasher>();
builder.Services.AddSingleton<RecordRepository>();
builder.Services.AddSingleton<BesuContractService>();
builder.Services.AddSingleton<HistoryService>();

var app = builder.Build();

if (args.Length == 1 && args[0].Equals("deploy", StringComparison.OrdinalIgnoreCase))
{
    var contractService = app.Services.GetRequiredService<BesuContractService>();
    await DeployContract.RunAsync(contractService);
    return;
}

app.MapGet("/", () => Results.Ok(new
{
    name = "dotnet-besu-hash-demo",
    endpoints = new[]
    {
        "POST /api/blockchain/anchor",
        "GET /api/blockchain/history",
        "GET /api/blockchain/records"
    }
}));

app.MapControllers();

app.Run();
