using Downloader.Abstraction.Interfaces.Model;
using Downloader.Abstraction.Interfaces.Services;
using Downloader.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Downloader.Service
{
    public class OrchestratorService : IOrchestratorService
    {
        private readonly ILogger<OrchestratorService> logger;
        private readonly IFileService fileService;
        private readonly IDownloadService downloadService;
        private readonly IReportService reportService;
        private readonly IOptions<DownloaderSettings> options;

        public OrchestratorService(
            ILogger<OrchestratorService> logger,
            IFileService fileService,
            IDownloadService downloadService,
            IReportService reportService,
            IOptions<DownloaderSettings> options)
        {
            this.logger = logger;
            this.fileService = fileService;
            this.downloadService = downloadService;
            this.reportService = reportService;
            this.options = options;
        }

        /// <inheritdoc />
        public async Task InitiateWorkflow()
        {
            logger.LogInformation("Workflow started.");

            var targets = await LoadTargets();
            if (targets.Count == 0)
            {
                logger.LogInformation("Workflow completed with no targets and no report.");
                return;
            }

            var results = await DownloadAllWithConcurrencyLimit(targets, options.Value.MaxConcurrentDownloads);

            var report = reportService.GenerateReport(results);
            await fileService.ExportReport(report);

            logger.LogInformation("Workflow completed. Downloads: {Count}", results.Count);
        }

        private async Task<IList<IDownloadTarget>> LoadTargets()
        {
            var targets = await fileService.LoadTargetsFromInput();
            return targets ?? throw new InvalidOperationException("LoadTargetsFromInput returned null.");
        }

        private async Task<IList<IDownloadTarget>> DownloadAllWithConcurrencyLimit(
            IList<IDownloadTarget> targets,
            int maxConcurrentDownloads)
        {
            var workQueue = BuildWorkQueue(targets);
            return await RunQueueWithLimit(workQueue, maxConcurrentDownloads);
        }

        private Queue<Func<Task<IDownloadTarget>>> BuildWorkQueue(IList<IDownloadTarget> targets)
        {
            var queue = new Queue<Func<Task<IDownloadTarget>>>(targets.Count);

            foreach (var target in targets)
            {
                var captured = target;
                queue.Enqueue(() => downloadService.DownloadContent(captured));
            }

            return queue;
        }

        private async Task<IList<IDownloadTarget>> RunQueueWithLimit(
            Queue<Func<Task<IDownloadTarget>>> queue,
            int maxConcurrent)
        {
            var active = new List<Task<IDownloadTarget>>(Math.Min(maxConcurrent, queue.Count));
            var completed = new List<IDownloadTarget>();

            StartInitialBatch(queue, active, maxConcurrent);

            while (active.Count > 0)
            {
                var finished = await Task.WhenAny(active);
                active.Remove(finished);

                // Fail-fast: exceptions bubble up here
                completed.Add(await finished);

                StartNextIfAvailable(queue, active);
            }

            return completed;
        }

        private void StartInitialBatch(
            Queue<Func<Task<IDownloadTarget>>> queue,
            List<Task<IDownloadTarget>> active,
            int maxConcurrent)
        {
            while (active.Count < maxConcurrent && queue.Count > 0)
            {
                active.Add(queue.Dequeue().Invoke());
            }
        }

        private void StartNextIfAvailable(
            Queue<Func<Task<IDownloadTarget>>> queue,
            List<Task<IDownloadTarget>> active)
        {
            if (queue.Count > 0)
            {
                active.Add(queue.Dequeue().Invoke());
            }
        }
    }
}