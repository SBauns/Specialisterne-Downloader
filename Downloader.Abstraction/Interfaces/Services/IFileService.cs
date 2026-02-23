using Downloader.Abstraction.Interfaces.Model;

namespace Downloader.Abstraction.Interfaces.Services
{
    public interface IFileService
    {
        Task<IList<IDownloadTarget>> LoadTargetsFromInput();
        Task ExportDownloadedFile(string fileName, object file);
        Task ExportReport(string content);
    }
}