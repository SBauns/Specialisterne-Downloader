using Downloader.Abstraction.Interfaces.Model;
using Downloader.Abstraction.Interfaces.Services;
using Downloader.Extensions;
using Downloader.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Downloader.Service
{
    public class DownloadService : IDownloadService
    {
        private readonly ILogger<DownloadService> logger;
        private readonly IFileService fileService;
        private readonly IOptions<DownloaderSettings> options;

        public DownloadService(ILogger<DownloadService> logger, IFileService fileService, IOptions<DownloaderSettings> options)
        {
            this.logger = logger;
            this.fileService = fileService;
            this.options = options;
        }

        /// <inheritdoc />
        public async Task<IDownloadTarget> DownloadContent(IDownloadTarget target)
        {
            using var scope = logger.BeginTargetScope(target);

            return target;
        }
    }
}