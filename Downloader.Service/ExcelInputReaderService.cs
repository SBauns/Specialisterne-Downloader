using ClosedXML.Excel;
using Downloader.Abstraction.Interfaces.Model;
using Downloader.Abstraction.Interfaces.Services;
using Downloader.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Downloader.Service
{
    public class ExcelInputReaderService : IInputReaderService
    {
        private readonly ILogger<ExcelInputReaderService> logger;
        private const int COL_OUTPUT_FILE_NAME = 1; // A
        private const int COL_PRIMARY_LINK = 38; // AL
        private const int COL_SECONDARY_LINK = 39; // AM

        public ExcelInputReaderService(ILogger<ExcelInputReaderService> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc />
        public Task<IList<IDownloadTarget>> LoadTargets(string sourceFile)
        {
            var path = ValidateExcelPath(sourceFile);

            using var workbook = OpenWorkbook(path);
            var worksheet = GetWorksheet(workbook);

            var rawTargets = ReadTargetsFromWorksheet(worksheet);
            var filteredTargets = FilterTargetsWithoutLinks(rawTargets);

            return Task.FromResult<IList<IDownloadTarget>>(filteredTargets);
        }

        private string ValidateExcelPath(string sourceFile)
        {
            return !File.Exists(sourceFile) ? throw new FileNotFoundException($"Excel input file not found: {sourceFile}",
                sourceFile) : sourceFile;
        }

        private XLWorkbook OpenWorkbook(string path)
            => new XLWorkbook(path);

        private IXLWorksheet GetWorksheet(XLWorkbook workbook)
            => workbook.Worksheets.First();

        private List<IDownloadTarget> ReadTargetsFromWorksheet(IXLWorksheet worksheet)
        {
            var lastRow = GetLastUsedRowNumber(worksheet);

            if (lastRow < 2)
            {
                logger.LogWarning("Excel input contains no data rows (only headers or empty sheet).");
                return new List<IDownloadTarget>();
            }

            var lastColumn = GetLastUsedColumnNumber(worksheet);
            var targets = new List<IDownloadTarget>();

            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);

                if (!RowHasAnyTextContent(row, lastColumn))
                    continue;

                if (!TryCreateTargetFromRow(row, rowNumber, out var target))
                    continue;

                targets.Add(target);
            }

            return targets;
        }

        private int GetLastUsedRowNumber(IXLWorksheet worksheet)
            => worksheet.LastRowUsed()?.RowNumber() ?? 0;

        private int GetLastUsedColumnNumber(IXLWorksheet worksheet)
            => worksheet.LastColumnUsed()?.ColumnNumber() ?? 1;

        /// <summary>
        /// Treat row as "having content" if ANY cell contains non-whitespace text.
        /// (Avoids "used cells" being affected by formatting.)
        /// </summary>
        /// <param name="row"></param>
        /// <param name="lastColumn"></param>
        /// <returns></returns>
        private bool RowHasAnyTextContent(IXLRow row, int lastColumn)
        {
            return row.Cells(1, lastColumn).Any(c => !string.IsNullOrWhiteSpace(c.GetString()));
        }

        private bool TryCreateTargetFromRow(IXLRow row, int rowNumber, out IDownloadTarget target)
        {
            var outputFileName = GetCellTrimmedOrEmpty(row, COL_OUTPUT_FILE_NAME);
            var primaryLink = GetCellTrimmedOrEmpty(row, COL_PRIMARY_LINK);
            var secondaryLink = GetCellTrimmedOrEmpty(row, COL_SECONDARY_LINK);

            if (string.IsNullOrWhiteSpace(outputFileName))
            {
                logger.LogWarning(
                    "Row {RowNumber} has data but cell A ({OutputFileName}) is empty. Skipping row.",
                    rowNumber, nameof(IDownloadTarget.OutputFileName));

                target = null!;
                return false;
            }

            target = new DownloadTarget
            {
                OutputFileName = outputFileName,
                PrimaryLink = primaryLink,
                SecondaryLink = secondaryLink,
                WasSuccessfullyDownloaded = false
            };

            return true;
        }

        private string GetCellTrimmedOrEmpty(IXLRow row, int columnNumber)
            => row.Cell(columnNumber).GetString()?.Trim() ?? string.Empty;

        private List<IDownloadTarget> FilterTargetsWithoutLinks(List<IDownloadTarget> targets)
        {
            if (targets.Count == 0)
                return targets;

            var filtered = new List<IDownloadTarget>(targets.Count);

            foreach (var target in targets)
            {
                if (HasAnyLink(target))
                {
                    filtered.Add(target);
                    continue;
                }

                logger.LogWarning(
                    "Removing target '{OutputFileName}' because neither {PrimaryLink} nor {SecondaryLink} is set.",
                    target.OutputFileName, nameof(IDownloadTarget.PrimaryLink), nameof(IDownloadTarget.SecondaryLink));
            }

            return filtered;
        }

        private bool HasAnyLink(IDownloadTarget t)
            => !string.IsNullOrWhiteSpace(t.PrimaryLink)
               || !string.IsNullOrWhiteSpace(t.SecondaryLink);
    }
}