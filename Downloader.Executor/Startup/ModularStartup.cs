using Downloader.Abstraction.Interfaces.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Downloader.Executor.Startup
{
    public abstract class ModularStartup : IStartupModule
    {
        protected ICollection<IStartupModule> _modules;

        protected ModularStartup()
        {
            _modules = new List<IStartupModule>();
        }

        public IServiceCollection Services { get; protected set; }
        public IServiceProvider ServiceProvider { get; protected set; }

        /// <inheritdoc />
        public virtual void ConfigureServices(HostBuilderContext hostBuilderContext, IServiceCollection services)
        {
        }

        protected void AddModule(IStartupModule module)
        {
            _modules.Add(module);
        }

        public void SetupServices(HostBuilderContext hostBuilderContext, IServiceCollection? services = null)
        {
            Services = services ??= new ServiceCollection();

            ConfigureServices(hostBuilderContext, services);
            foreach (IStartupModule module in _modules)
                module.ConfigureServices(hostBuilderContext, Services);

            ServiceProvider = Services.BuildServiceProvider();
        }
    }
}