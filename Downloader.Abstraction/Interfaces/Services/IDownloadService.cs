using Downloader.Abstraction.Interfaces.Model;

namespace Downloader.Abstraction.Interfaces.Services
{
    public interface IDownloadService
    {
        Task<IDownloadTarget> DownloadContent(IDownloadTarget target);
    }
}