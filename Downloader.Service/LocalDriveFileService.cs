using ClosedXML.Excel;
using Downloader.Abstraction.Interfaces.Model;
using Downloader.Abstraction.Interfaces.Services;
using Downloader.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Downloader.Service
{
    public class LocalDriveFileService : IFileService
    {
        private readonly ILogger<LocalDriveFileService> logger;
        private readonly IInputReaderService inputReader;
        private readonly IOptions<DownloaderSettings> options;

        public LocalDriveFileService(ILogger<LocalDriveFileService> logger, IInputReaderService inputReader, IOptions<DownloaderSettings> options)
        {
            this.logger = logger;
            this.inputReader = inputReader;
            this.options = options;
        }

        /// <inheritdoc />
        public async Task<IList<IDownloadTarget>> LoadTargetsFromInput()
        {
            var inputSourceFile = options.Value.FilesToDownloadExcelInput;

            return await inputReader.LoadTargets(inputSourceFile);
        }

        /// <inheritdoc />
        public Task ExportDownloadedFile(string fileName, object file)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task ExportReport(string content)
        {
            throw new NotImplementedException();
        }
    }
}