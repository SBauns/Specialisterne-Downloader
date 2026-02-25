using ClosedXML.Excel;
using Downloader.Abstraction.Enum;
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
        private readonly IOptions<DownloaderSettings> options;
        private const int COL_OUTPUT_FILE_NAME = 1; // A
        private const int COL_PRIMARY_LINK = 38; // AL
        private const int COL_SECONDARY_LINK = 39; // AM

        public ExcelInputReaderService(ILogger<ExcelInputReaderService> logger, IOptions<DownloaderSettings> options)
        {
            this.logger = logger;
            this.options = options;
        }

        /// <inheritdoc />
        public Task<IList<IDownloadTarget>> LoadTargets(string sourceFile)
        {
            var lowerBound = options.Value.TargetStartIndex;
            var upperBound = options.Value.TargetEndIndex;

            var path = ValidateExcelPath(sourceFile);

            using var workbook = OpenWorkbook(path);
            var worksheet = GetWorksheet(workbook);

            var targets = ReadTargetsFromWorksheet(worksheet);
            var filteredTargets = ApplyTargetRange(targets, lowerBound, upperBound);

            return Task.FromResult<IList<IDownloadTarget>>(filteredTargets);
        }

        private List<IDownloadTarget> ApplyTargetRange(List<IDownloadTarget> targets, int lowerBound, int upperBound)
        {
            if (targets.Count == 0)
                return targets;

            var start = lowerBound < 0 ? 0 : lowerBound;
            var end = upperBound < 0 ? targets.Count - 1 : upperBound;

            if (end < start)
            {
                logger.LogWarning(
                    "Invalid target range. StartIndex: {StartIndex}, EndIndex: {EndIndex}. Returning 0 targets.",
                    lowerBound, upperBound);

                return new List<IDownloadTarget>();
            }

            // Clamp to available targets
            start = Math.Clamp(start, 0, targets.Count - 1);
            end = Math.Clamp(end, 0, targets.Count - 1);

            var count = end - start + 1;

            logger.LogInformation(
                "Applying target range. Requested: [{RequestedStart}..{RequestedEnd}] -> Applied: [{AppliedStart}..{AppliedEnd}] Count: {Count} out of {Total}.",
                lowerBound, upperBound, start, end, count, targets.Count);

            return targets.Skip(start).Take(count).ToList();
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
                DownloadedUsing = DownloadedUsing.NONE,
            };

            return true;
        }

        private string GetCellTrimmedOrEmpty(IXLRow row, int columnNumber)
            => row.Cell(columnNumber).GetString()?.Trim() ?? string.Empty;
    }
}