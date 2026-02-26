using ClosedXML.Excel;
using Downloader.Abstraction.Enum;
using Downloader.Model;
using Downloader.Service;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Downloader.Tests.Service
{
    [TestFixture]
    public class ExcelInputReaderServiceTests
    {
        private ILogger<ExcelInputReaderService> logger = null!;

        [SetUp]
        public void SetUp()
        {
            logger = Mock.Of<ILogger<ExcelInputReaderService>>();
        }

        [Test]
        public async Task LoadTargets_WhenFileDoesNotExist_ThrowsFileNotFoundException()
        {
            // Arrange
            var options = Options.Create(new DownloaderSettings
            {
                TargetStartIndex = -1,
                TargetEndIndex = -1,
            });

            var sut = new ExcelInputReaderService(logger, options);
            var missingPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".xlsx");

            // Act
            Func<Task> act = async () => await sut.LoadTargets(missingPath);

            // Assert
            var ex = await act.Should().ThrowAsync<FileNotFoundException>();
            ex.Which.FileName.Should().Be(missingPath);
            ex.Which.Message.Should().Contain("Excel input file not found");
        }

        [Test]
        public async Task LoadTargets_WhenSheetHasOnlyHeaderRow_ReturnsEmptyList()
        {
            // Arrange
            var path = CreateTempExcelFile(ws =>
            {
                ws.Cell(1, ExcelInputReaderService.OutputFileNameColumnIndex).Value = "OutputFileName";
                ws.Cell(1, ExcelInputReaderService.PrimaryLinkColumnIndex).Value = "PrimaryLink";
                ws.Cell(1, ExcelInputReaderService.SecondaryLinkColumnIndex).Value = "SecondaryLink";
            });

            var options = Options.Create(new DownloaderSettings
            {
                TargetStartIndex = -1,
                TargetEndIndex = -1,
            });

            var sut = new ExcelInputReaderService(logger, options);

            // Act
            var targets = await sut.LoadTargets(path);

            // Assert
            targets.Should().NotBeNull();
            targets.Should().BeEmpty();
        }

        [Test]
        public async Task LoadTargets_SkipsRowsWithNoTextContent()
        {
            // Arrange
            var path = CreateTempExcelFile(ws =>
            {
                // Header
                ws.Cell(1, ExcelInputReaderService.OutputFileNameColumnIndex).Value = "OutputFileName";

                // Row 2: all empty => should be skipped

                // Row 3: whitespace only => should be skipped
                ws.Cell(3, 2).Value = "   ";

                // Row 4: valid row
                ws.Cell(4, ExcelInputReaderService.OutputFileNameColumnIndex).Value = "FileA";
                ws.Cell(4, ExcelInputReaderService.PrimaryLinkColumnIndex).Value = "https://primary/a";
                ws.Cell(4, ExcelInputReaderService.SecondaryLinkColumnIndex).Value = "https://secondary/a";
            });

            var options = Options.Create(new DownloaderSettings
            {
                TargetStartIndex = -1,
                TargetEndIndex = -1,
            });

            var sut = new ExcelInputReaderService(logger, options);

            // Act
            var targets = (await sut.LoadTargets(path)).ToList();

            // Assert
            targets.Should().HaveCount(1);
            targets[0].OutputFileName.Should().Be("FileA");
        }

        [Test]
        public async Task LoadTargets_WhenRowHasContentButMissingOutputFileName_SkipsRow()
        {
            // Arrange
            var path = CreateTempExcelFile(ws =>
            {
                // Header
                ws.Cell(1, ExcelInputReaderService.OutputFileNameColumnIndex).Value = "OutputFileName";

                // Row 2: has other content, but A is empty => should be skipped
                ws.Cell(2, 2).Value = "some data";

                // Row 3: valid
                ws.Cell(3, ExcelInputReaderService.OutputFileNameColumnIndex).Value = "FileB";
                ws.Cell(3, ExcelInputReaderService.PrimaryLinkColumnIndex).Value = "https://primary/b";
            });

            var options = Options.Create(new DownloaderSettings
            {
                TargetStartIndex = -1,
                TargetEndIndex = -1,
            });

            var sut = new ExcelInputReaderService(logger, options);

            // Act
            var targets = (await sut.LoadTargets(path)).ToList();

            // Assert
            targets.Should().HaveCount(1);
            targets[0].OutputFileName.Should().Be("FileB");
            targets[0].PrimaryLink.Should().Be("https://primary/b");
        }

        [Test]
        public async Task LoadTargets_MapsColumnsCorrectly_AndTrimsValues()
        {
            // Arrange
            var path = CreateTempExcelFile(ws =>
            {
                ws.Cell(1, ExcelInputReaderService.OutputFileNameColumnIndex).Value = "OutputFileName";

                ws.Cell(2, ExcelInputReaderService.OutputFileNameColumnIndex).Value = "  MyFile  ";
                ws.Cell(2, ExcelInputReaderService.PrimaryLinkColumnIndex).Value = "  https://primary/link  ";
                ws.Cell(2, ExcelInputReaderService.SecondaryLinkColumnIndex).Value = "  https://secondary/link  ";
            });

            var options = Options.Create(new DownloaderSettings
            {
                TargetStartIndex = -1,
                TargetEndIndex = -1,
            });

            var sut = new ExcelInputReaderService(logger, options);

            // Act
            var target = (await sut.LoadTargets(path)).Single();

            // Assert
            target.OutputFileName.Should().Be("MyFile");
            target.PrimaryLink.Should().Be("https://primary/link");
            target.SecondaryLink.Should().Be("https://secondary/link");
            target.DownloadedUsing.Should().Be(DownloadedUsing.NONE);
        }

        [Test]
        public async Task LoadTargets_WhenRangeIsNegative_ReturnsAllTargets()
        {
            // Arrange
            var path = CreateTempExcelFile(ws => WriteTargets(ws,
                ("A", "pA", "sA"),
                ("B", "pB", "sB"),
                ("C", "pC", "sC")));

            var options = Options.Create(new DownloaderSettings
            {
                TargetStartIndex = -1,
                TargetEndIndex = -1,
            });

            var sut = new ExcelInputReaderService(logger, options);

            // Act
            var targets = (await sut.LoadTargets(path)).Select(t => t.OutputFileName).ToList();

            // Assert
            targets.Should().Equal("A", "B", "C");
        }

        [Test]
        public async Task LoadTargets_WhenRangeSpecified_ReturnsInclusiveSlice()
        {
            // Arrange
            // Targets read as index-based: 0=A,1=B,2=C,3=D,4=E
            var path = CreateTempExcelFile(ws => WriteTargets(ws,
                ("A", "pA", "sA"),
                ("B", "pB", "sB"),
                ("C", "pC", "sC"),
                ("D", "pD", "sD"),
                ("E", "pE", "sE")));

            var options = Options.Create(new DownloaderSettings
            {
                TargetStartIndex = 1,
                TargetEndIndex = 3,
            });

            var sut = new ExcelInputReaderService(logger, options);

            // Act
            var targets = (await sut.LoadTargets(path)).Select(t => t.OutputFileName).ToList();

            // Assert
            targets.Should().Equal("B", "C", "D");
        }

        [Test]
        public async Task LoadTargets_WhenEndIsBeforeStart_ReturnsEmpty()
        {
            // Arrange
            var path = CreateTempExcelFile(ws => WriteTargets(ws,
                ("A", "pA", "sA"),
                ("B", "pB", "sB"),
                ("C", "pC", "sC")));

            var options = Options.Create(new DownloaderSettings
            {
                TargetStartIndex = 2,
                TargetEndIndex = 1,
            });

            var sut = new ExcelInputReaderService(logger, options);

            // Act
            var targets = await sut.LoadTargets(path);

            // Assert
            targets.Should().BeEmpty();
        }

        [Test]
        public async Task LoadTargets_WhenRangeExceedsBounds_IsClamped()
        {
            // Arrange
            var path = CreateTempExcelFile(ws => WriteTargets(ws,
                ("A", "pA", "sA"),
                ("B", "pB", "sB"),
                ("C", "pC", "sC")));

            var options = Options.Create(new DownloaderSettings
            {
                TargetStartIndex = -100, // clamps to 0
                TargetEndIndex = 999,    // clamps to last
            });

            var sut = new ExcelInputReaderService(logger, options);

            // Act
            var targets = (await sut.LoadTargets(path)).Select(t => t.OutputFileName).ToList();

            // Assert
            targets.Should().Equal("A", "B", "C");
        }

        // ---------- Helpers ----------

        private static string CreateTempExcelFile(Action<IXLWorksheet> configure)
        {
            var tempDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "test-excels");
            Directory.CreateDirectory(tempDir);

            var path = Path.Combine(tempDir, $"{Guid.NewGuid():N}.xlsx");

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Sheet1");

            configure(ws);

            wb.SaveAs(path);
            return path;
        }

        private static void WriteTargets(IXLWorksheet ws, params (string output, string primary, string secondary)[] rows)
        {
            // Header row (service reads from row 2)
            ws.Cell(1, ExcelInputReaderService.OutputFileNameColumnIndex).Value = "OutputFileName";
            ws.Cell(1, ExcelInputReaderService.PrimaryLinkColumnIndex).Value = "PrimaryLink";
            ws.Cell(1, ExcelInputReaderService.SecondaryLinkColumnIndex).Value = "SecondaryLink";

            var rowNumber = 2;
            foreach (var (output, primary, secondary) in rows)
            {
                ws.Cell(rowNumber, ExcelInputReaderService.OutputFileNameColumnIndex).Value = output;
                ws.Cell(rowNumber, ExcelInputReaderService.PrimaryLinkColumnIndex).Value = primary;
                ws.Cell(rowNumber, ExcelInputReaderService.SecondaryLinkColumnIndex).Value = secondary;
                rowNumber++;
            }
        }
    }
}