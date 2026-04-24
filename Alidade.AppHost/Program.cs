namespace Alidade.AppHost;

/// <summary>
///   Entry point for the Aspire AppHost.
///   Used for local development and testing, not intended for production use.
/// </summary>
public static class Program
{
    /// <summary>
    ///   The main character of the project.
    /// </summary>
    /// <param name="args">Arg, I'm a pirate.</param>
    public static async Task Main(string[] args)
    {
        await DistributedApplication.CreateBuilder(args).BuildAppHost().RunAppHostAsync();
    }

    private static DistributedApplication BuildAppHost(this IDistributedApplicationBuilder builder)
    {
        builder.AddProject<Projects.Alidade>("alidade")
               .WithExternalHttpEndpoints();

        return builder.Build();
    }

    private static async Task RunAppHostAsync(this DistributedApplication app)
    {
        await app.RunAsync();
    }
}
