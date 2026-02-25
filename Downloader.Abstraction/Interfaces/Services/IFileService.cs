using Downloader.Abstraction.Interfaces.Model;

namespace Downloader.Abstraction.Interfaces.Services
{
    public interface IFileService
    {
        Task<IList<IDownloadTarget>> LoadTargetsFromInput();
        Task ExportDownloadedFile(IDownloadTarget target, Stream fileStream);
        Task ExportReport(string content, string fileExtension);
    }
}