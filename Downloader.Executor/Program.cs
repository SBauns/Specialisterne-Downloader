using Downloader.Abstraction.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Downloader.Executor
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var startup = new ExeStartup();
            using var host = startup.BuildHost(args);
            using var scope = host.Services.CreateScope();

            var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestratorService>();

            await orchestrator.InitiateWorkflow();
        }
    }
}
