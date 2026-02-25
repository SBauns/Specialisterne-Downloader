using Downloader.Abstraction.Interfaces.Model;
using Downloader.Abstraction.Interfaces.Services;
using Downloader.Extensions;
using Downloader.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Downloader.Service
{
    public class DownloadService : IDownloadService
    {
        private const string PRIMARY_LINK_LABEL = "Primary";
        private const string SECONDARY_LINK_LABEL = "Secondary";

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

            var maxDownloadRetries = options.Value.DownloadRetries;
            var secondsBetweenRetries = options.Value.SecondsWaitBetweenRetry;

            logger.LogInformation("Starting download. Retries: {MaxRetries}, WaitBetweenRetriesSeconds: {WaitSeconds}",
                maxDownloadRetries, secondsBetweenRetries);

            if (await TryDownloadAndExport(target, target.PrimaryLink, PRIMARY_LINK_LABEL, maxDownloadRetries,
                    secondsBetweenRetries))
                return target;

            logger.LogInformation("Primary link failed. Falling back to Secondary link.");

            if (await TryDownloadAndExport(target, target.SecondaryLink, SECONDARY_LINK_LABEL, maxDownloadRetries,
                    secondsBetweenRetries))
                return target;

            target.WasSuccessfullyDownloaded = false;
            target.TimeToDownload = null;

            logger.LogWarning("Download failed for both Primary and Secondary links.");
            return target;
        }

        private async Task<bool> TryDownloadAndExport(
    IDownloadTarget target,
    string? link,
    string linkLabel,
    int maxDownloadRetries,
    int secondsBetweenRetries)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                logger.LogDebug("No {LinkLabel} link provided (empty). Treating as failure.", linkLabel);
                return false;
            }

            var result = await TryDownloadWithRetries(link, linkLabel, maxDownloadRetries, secondsBetweenRetries);
            if (!result.Succeeded)
                return false;

            target.WasSuccessfullyDownloaded = true;
            target.TimeToDownload = result.TimeToDownload;

            await using (result.FileStream)
            {
                logger.LogDebug("Exporting downloaded file. Source: {Link}", link);
                await fileService.ExportDownloadedFile(target.OutputFileName, link, result.FileStream!);
            }

            logger.LogInformation(
                "Download and export completed. Used Link: {LinkLabel}, Time To Download: {TimeToDownload}",
                linkLabel,
                target.TimeToDownload);

            return true;
        }

        private async Task<DownloadAttemptResult> TryDownloadWithRetries(
            string link, string linkLabel, int maxDownloadRetries, int secondsBetweenRetries)
        {
            using var httpClient = new HttpClient();

            for (var attempt = 1; attempt <= maxDownloadRetries; attempt++)
            {
                try
                {
                    return await HandleSuccessfulAttempt(httpClient, link, linkLabel, attempt, maxDownloadRetries);
                }
                catch (Exception ex)
                {
                    var shouldRetry = await HandleFailedAttempt(ex, linkLabel, attempt, maxDownloadRetries,
                        secondsBetweenRetries);

                    if (!shouldRetry)
                        break;
                }
            }

            logger.LogDebug("All retries exhausted for {LinkLabel} link.", linkLabel);
            return DownloadAttemptResult.Failure();
        }

        private async Task<DownloadAttemptResult> HandleSuccessfulAttempt(
            HttpClient httpClient, string link, string linkLabel, int attempt, int maxDownloadRetries)
        {
            logger.LogDebug("Attempting download ({Attempt}/{Max}) using {LinkLabel} link.", attempt,
                maxDownloadRetries, linkLabel);

            var (stream, elapsed) = await DownloadOnce(httpClient, link);

            logger.LogInformation(
                "Download attempt succeeded ({Attempt}/{Max}) using {LinkLabel} link. Elapsed: {Elapsed}", attempt,
                maxDownloadRetries, linkLabel, elapsed);

            return DownloadAttemptResult.Success(stream, elapsed);
        }

        private async Task<bool> HandleFailedAttempt(
            Exception ex, string linkLabel, int attempt, int maxDownloadRetries, int secondsBetweenRetries)
        {
            logger.LogWarning(ex, "Download attempt failed ({Attempt}/{Max}) using {LinkLabel} link.", attempt,
                maxDownloadRetries, linkLabel);

            if (attempt >= maxDownloadRetries)
                return false;

            logger.LogDebug("Waiting {SecondsBetweenRetries}s before retrying download.", secondsBetweenRetries);

            await Task.Delay(TimeSpan.FromSeconds(secondsBetweenRetries));

            return true;
        }

        private async Task<(Stream Stream, TimeSpan Elapsed)> DownloadOnce(HttpClient httpClient, string link)
        {
            var sw = Stopwatch.StartNew();

            using var response = await httpClient.GetAsync(link, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            // Buffer fully so the timing reflects the full download of the attempt.
            var bytes = await response.Content.ReadAsByteArrayAsync();

            sw.Stop();

            var ms = new MemoryStream(bytes, writable: false);
            ms.Position = 0;

            return (ms, sw.Elapsed);
        }

        private sealed record DownloadAttemptResult(bool Succeeded, Stream? FileStream, TimeSpan? TimeToDownload)
        {
            public static DownloadAttemptResult Success(Stream stream, TimeSpan elapsed) => new(true, stream, elapsed);
            public static DownloadAttemptResult Failure() => new(false, null, null);
        }
    }
}