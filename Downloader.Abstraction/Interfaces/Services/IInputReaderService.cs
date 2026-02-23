using Downloader.Abstraction.Interfaces.Model;

namespace Downloader.Abstraction.Interfaces.Services
{
    public interface IInputReaderService
    {
        Task<IList<IDownloadTarget>> LoadTargets(string sourceFile);
    }
}