using Downloader.Abstraction.Enum;
using Downloader.Abstraction.Interfaces.Model;
using Downloader.Abstraction.Interfaces.Services;
using Downloader.Extensions;
using Downloader.Model;
using FluentMarkdown;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Downloader.Service
{
    public class MarkdownReportService : IReportService
    {
        private readonly ILogger<MarkdownReportService> logger;
        private readonly IOptions<DownloaderSettings> options;

        public MarkdownReportService(ILogger<MarkdownReportService> logger, IOptions<DownloaderSettings> options)
        {
            this.logger = logger;
            this.options = options;
        }

        /// <inheritdoc />
        public string GenerateReport(IList<IDownloadTarget> targets)
        {
            if (targets is null)
                throw new ArgumentNullException(nameof(targets));

            var downloadExportPath = options.Value.DownloadedFilesOutputPath;

            var createdAt = DateTime.Now;
            var createdAtText = createdAt.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

            logger.LogInformation(
                "Generating markdown report. Targets: {TargetCount}, DownloadExportPath: {DownloadExportPath}",
                targets.Count,
                downloadExportPath);

            var (primary, secondary, none) = SplitTargets(targets);

            logger.LogDebug(
                "Report target groups. Primary: {PrimaryCount}, Secondary: {SecondaryCount}, None: {NoneCount}",
                primary.Count,
                secondary.Count,
                none.Count);

            var builder = new MarkdownBuilder();

            AddHeaderAndMetadata(builder, createdAtText, downloadExportPath);
            AddPrimarySection(builder, primary);
            AddSecondarySection(builder, secondary);
            AddNoneSection(builder, none);

            return builder.ToString();
        }

        private (List<IDownloadTarget> Primary, List<IDownloadTarget> Secondary, List<IDownloadTarget> None)
            SplitTargets(IList<IDownloadTarget> targets)
        {
            var primary = new List<IDownloadTarget>();
            var secondary = new List<IDownloadTarget>();
            var none = new List<IDownloadTarget>();

            foreach (var t in targets)
            {
                switch (t.DownloadedUsing)
                {
                    case DownloadedUsing.PRIMARY:
                        primary.Add(t);
                        break;
                    case DownloadedUsing.SECONDARY:
                        secondary.Add(t);
                        break;
                    default:
                        none.Add(t);
                        break;
                }
            }

            return (primary, secondary, none);
        }

        private void AddHeaderAndMetadata(MarkdownBuilder builder, string createdAtText, string downloadExportPath)
        {
            builder
                .AddHeader(1, "Download Report")
                .AddParagraph($"Created: {createdAtText}")
                .AddParagraph($"Downloaded files output path: `{downloadExportPath}`");
        }

        private void AddPrimarySection(MarkdownBuilder builder, IList<IDownloadTarget> primary)
        {
            builder.AddHeader(2, $"Downloaded using {nameof(DownloadedUsing.PRIMARY).ToTitleFromScreamingSnakeCase()}");

            builder.AddUnorderedList(ul =>
            {
                foreach (var t in primary)
                {
                    var line = FormatSuccessLine(
                        link: t.PrimaryLink,
                        fullOutputFileName: t.FullOutputFileName!,
                        timeToDownload: t.TimeToDownload);

                    ul.AddTaskListItem(line, isChecked: true);
                }
            });
        }

        private void AddSecondarySection(MarkdownBuilder builder, IList<IDownloadTarget> secondary)
        {
            builder.AddHeader(2, $"Downloaded using {nameof(DownloadedUsing.SECONDARY).ToTitleFromScreamingSnakeCase()}");

            builder.AddUnorderedList(ul =>
            {
                foreach (var t in secondary)
                {
                    var line = FormatSuccessLine(
                        link: t.SecondaryLink,
                        fullOutputFileName: t.FullOutputFileName!,
                        timeToDownload: t.TimeToDownload);

                    ul.AddTaskListItem(line, isChecked: true);
                }
            });
        }

        private void AddNoneSection(MarkdownBuilder builder, IList<IDownloadTarget> none)
        {
            builder.AddHeader(2, "Not downloaded");

            builder.AddUnorderedList(ul =>
            {
                foreach (var t in none)
                {
                    ul.AddTaskListItem(t.OutputFileName, isChecked: false);
                }
            });
        }

        private string FormatSuccessLine(string? link, string fullOutputFileName, TimeSpan? timeToDownload)
        {
            if (string.IsNullOrWhiteSpace(link))
                link = "<missing link>";

            var fileName = Path.GetFileName(fullOutputFileName);

            var ms = timeToDownload?.TotalMilliseconds;

            var msText = ms is null ? "unknown" : $"{ms.Value:0} ms";

            return $"{msText,8}  {fileName,-12}  {link}";
        }

        /// <inheritdoc />
        public string GetOutputFileExtension()
        {
            return ".md";
        }
    }
}