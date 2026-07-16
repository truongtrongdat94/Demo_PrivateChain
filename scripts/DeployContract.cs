namespace HashAnchorDemo;
public static class DeployContract
{
    public static async Task RunAsync(BesuContractService contractService)
    {
        var deployment = await contractService.DeployAsync(CancellationToken.None);

        Console.WriteLine($"Contract address: {deployment.ContractAddress}");
        Console.WriteLine($"Transaction hash: {deployment.TransactionHash}");
        Console.WriteLine($"Block number: {deployment.BlockNumber}");
    }
}
