using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Downloader.Abstraction.Interfaces.Startup
{
    public interface IStartupModule
    {
        /// <summary>
        ///     To be called during call to 'SetupServices', wherein different services are configured.
        /// </summary>
        /// <param name="hostBuilderContext"></param>
        /// <param name="services"></param>
        void ConfigureServices(HostBuilderContext hostBuilderContext, IServiceCollection services);
    }
}