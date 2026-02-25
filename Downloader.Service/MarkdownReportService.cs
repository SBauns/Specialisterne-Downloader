using Downloader.Abstraction.Interfaces.Model;
using Downloader.Abstraction.Interfaces.Services;

namespace Downloader.Service
{
    public class MarkdownReportService : IReportService
    {
        /// <inheritdoc />
        public string GenerateReport(IList<IDownloadTarget> targets)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public string GetOutputFileExtension()
        {
            return ".md";
        }
    }
}