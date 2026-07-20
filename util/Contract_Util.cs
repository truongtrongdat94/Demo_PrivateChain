using System.Numerics;
using System.Text.Json;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;

namespace HashAnchorDemo;

public sealed class HashRegistryDeployment : ContractDeploymentMessage
{
    public HashRegistryDeployment() : base(LoadByteCode())
    {
    }

    private static string LoadByteCode()
    {
        var artifactPath = Path.Combine(AppContext.BaseDirectory, "HashRegistry.json");
        using var artifact = JsonDocument.Parse(File.ReadAllText(artifactPath));
        return artifact.RootElement.GetProperty("bytecode").GetString()
            ?? throw new InvalidOperationException("HashRegistry bytecode is missing");
    }
}

[Function("anchorHash")]
public sealed class AnchorHashFunction : FunctionMessage
{
    [Parameter("bytes32", "payloadHash", 1)]
    public byte[] PayloadHash { get; init; } = Array.Empty<byte>();
}

[Event("HashAnchored")]
public sealed class HashAnchoredEventDto : IEventDTO
{
    [Parameter("uint256", "sequence", 1, true)]
    public BigInteger Sequence { get; set; }

    [Parameter("bytes32", "payloadHash", 2, true)]
    public byte[] PayloadHash { get; set; } = Array.Empty<byte>();

    [Parameter("address", "submitter", 3, true)]
    public string Submitter { get; set; } = string.Empty;

    [Parameter("uint64", "anchoredAt", 4, false)]
    public BigInteger AnchoredAt { get; set; }
}
