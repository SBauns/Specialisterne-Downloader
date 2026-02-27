using Downloader.Abstraction.Interfaces.Model;
using Downloader.Abstraction.Interfaces.Services;
using Downloader.Extensions;
using Downloader.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Downloader.Abstraction.Enum;

namespace Downloader.Service
{
    public class DownloadService : IDownloadService
    {
        private readonly ILogger<DownloadService> logger;
        private readonly IFileService fileService;
        private readonly IHttpFileDownloaderService fileDownloaderService;
        private readonly IOptions<DownloaderSettings> options;

        public DownloadService(
            ILogger<DownloadService> logger, IFileService fileService, IHttpFileDownloaderService fileDownloaderService,
            IOptions<DownloaderSettings> options)
        {
            this.logger = logger;
            this.fileService = fileService;
            this.fileDownloaderService = fileDownloaderService;
            this.options = options;
        }

        /// <inheritdoc />
        public async Task<IDownloadTarget> DownloadContent(IDownloadTarget target)
        {
            using IDisposable scope = logger.BeginTargetScope(target);

            int maxDownloadTries = options.Value.DownloadRetries + 1;
            int secondsBetweenRetries = options.Value.SecondsWaitBetweenRetry;

            logger.LogInformation("Starting download. Retries: {MaxRetries}, WaitBetweenRetriesSeconds: {WaitSeconds}",
                maxDownloadTries, secondsBetweenRetries);

            if (await TryDownloadAndExport(target, target.PrimaryLink, DownloadedUsing.PRIMARY, maxDownloadTries,
                    secondsBetweenRetries))
                return target;

            logger.LogInformation("Primary link failed. Falling back to Secondary link.");

            if (await TryDownloadAndExport(target, target.SecondaryLink, DownloadedUsing.SECONDARY, maxDownloadTries,
                    secondsBetweenRetries))
                return target;

            target.DownloadedUsing = DownloadedUsing.NONE;
            target.TimeToDownload = null;

            logger.LogWarning("Download failed for both Primary and Secondary links.");
            return target;
        }

        private async Task<bool> TryDownloadAndExport(
            IDownloadTarget target, string? link, DownloadedUsing targetDownloadedUsing, int maxDownloadTries,
            int secondsBetweenRetries)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                logger.LogDebug("No {LinkLabel} link provided (empty). Treating as failure.",
                    targetDownloadedUsing.ToString().ToTitleFromScreamingSnakeCase());
                return false;
            }

            DownloadAttemptResult result = await TryDownloadWithRetries(link, targetDownloadedUsing, maxDownloadTries,
                secondsBetweenRetries);
            if (!result.Succeeded)
                return false;

            target.DownloadedUsing = result.DownloadedUsing;
            target.TimeToDownload = result.TimeToDownload;

            await using (result.FileStream)
            {
                logger.LogDebug("Exporting downloaded file. Source: {Link}", link);
                await fileService.ExportDownloadedFile(target, result.FileStream!);
            }

            string formattedTime = target.TimeToDownload is null
                ? "Unknown"
                : target.TimeToDownload.Value.ToString(@"mm\:ss\.ff");
            logger.LogInformation(
                "Download and export completed. Used Link: {LinkLabel}, Time To Download: {TimeToDownload}",
                targetDownloadedUsing.ToString().ToTitleFromScreamingSnakeCase(), formattedTime);

            return true;
        }

        private async Task<DownloadAttemptResult> TryDownloadWithRetries(
            string link, DownloadedUsing downloadedUsing, int maxDownloadTries, int secondsBetweenRetries)
        {
            for (var attempt = 1; attempt <= maxDownloadTries; attempt++)
            {
                try
                {
                    return await HandleSuccessfulAttempt(link, downloadedUsing, attempt, maxDownloadTries);
                }
                catch (Exception ex)
                {
                    bool shouldRetry = await HandleFailedAttempt(ex, downloadedUsing, attempt, maxDownloadTries,
                        secondsBetweenRetries);

                    if (!shouldRetry)
                        break;
                }
            }

            logger.LogDebug("All retries exhausted for {LinkLabel} link.",
                downloadedUsing.ToString().ToTitleFromScreamingSnakeCase());
            return DownloadAttemptResult.Failure();
        }

        private async Task<DownloadAttemptResult> HandleSuccessfulAttempt(
            string link, DownloadedUsing downloadedUsing, int attempt, int maxDownloadRetries)
        {
            logger.LogDebug("Attempting download ({Attempt}/{Max}) using {LinkLabel} link.", attempt,
                maxDownloadRetries, downloadedUsing.ToString().ToTitleFromScreamingSnakeCase());

            (Stream stream, TimeSpan elapsed) = await fileDownloaderService.DownloadOnce(link);

            logger.LogInformation(
                "Download attempt succeeded ({Attempt}/{Max}) using {LinkLabel} link. Elapsed: {Elapsed}", attempt,
                maxDownloadRetries, downloadedUsing.ToString().ToTitleFromScreamingSnakeCase(), elapsed);

            return DownloadAttemptResult.Success(stream, elapsed, downloadedUsing);
        }

        private async Task<bool> HandleFailedAttempt(
            Exception ex, DownloadedUsing downloadedUsing, int attempt, int maxDownloadTries,
            int secondsBetweenRetries)
        {
            logger.LogTrace(ex, "Download attempt failed ({Attempt}/{Max}) using {LinkLabel} link.", attempt,
                maxDownloadTries, downloadedUsing.ToString().ToTitleFromScreamingSnakeCase());
            logger.LogInformation("Download attempt failed ({Attempt}/{Max}) using {LinkLabel} link.", attempt,
                maxDownloadTries, downloadedUsing.ToString().ToTitleFromScreamingSnakeCase());

            if (attempt >= maxDownloadTries)
                return false;

            logger.LogDebug("Waiting {SecondsBetweenRetries}s before retrying download.", secondsBetweenRetries);

            await Task.Delay(TimeSpan.FromSeconds(secondsBetweenRetries));

            return true;
        }

        private sealed record DownloadAttemptResult(
            bool Succeeded,
            Stream? FileStream,
            TimeSpan? TimeToDownload,
            DownloadedUsing DownloadedUsing)
        {
            public static DownloadAttemptResult Success(Stream stream, TimeSpan elapsed, DownloadedUsing linkLabel)
            {
                return new DownloadAttemptResult(true, stream, elapsed, linkLabel);
            }

            public static DownloadAttemptResult Failure()
            {
                return new DownloadAttemptResult(false, null, null, DownloadedUsing.NONE);
            }
        }
    }
}