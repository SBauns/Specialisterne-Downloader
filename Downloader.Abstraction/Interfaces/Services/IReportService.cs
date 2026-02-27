using Downloader.Abstraction.Interfaces.Model;

namespace Downloader.Abstraction.Interfaces.Services
{
    public interface IReportService
    {
        string GenerateReport(IList<IDownloadTarget> targets, TimeSpan? timeSpentDownloading);
        string GetOutputFileExtension();
    }
}