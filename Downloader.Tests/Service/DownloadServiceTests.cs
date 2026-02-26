using System;
using System.IO;
using System.Threading.Tasks;
using Downloader.Abstraction.Enum;
using Downloader.Abstraction.Interfaces.Model;
using Downloader.Abstraction.Interfaces.Services;
using Downloader.Model;
using Downloader.Service;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace Downloader.Tests.Service
{
    [TestFixture]
    public class DownloadServiceTests
    {
        private ILogger<DownloadService> logger = null!;
        private Mock<IFileService> fileServiceMock = null!;
        private Mock<IHttpFileDownloaderService> httpDownloaderMock = null!;

        [SetUp]
        public void SetUp()
        {
            logger = Mock.Of<ILogger<DownloadService>>();
            fileServiceMock = new Mock<IFileService>();
            httpDownloaderMock = new Mock<IHttpFileDownloaderService>();
        }

        [Test]
        public async Task DownloadContent_WhenPrimarySucceeds_DoesNotTrySecondary_ExportsAndSetsTarget()
        {
            // Arrange
            var targetMock = CreateTarget(primary: "https://primary", secondary: "https://secondary");
            var target = targetMock.Object;

            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var elapsed = TimeSpan.FromMilliseconds(123);

            httpDownloaderMock
                .Setup(d => d.DownloadOnce("https://primary"))
                .ReturnsAsync((stream, elapsed));

            var options = CreateOptions(downloadRetries: 3, secondsBetweenRetries: 0);
            var sut = new DownloadService(logger, fileServiceMock.Object, httpDownloaderMock.Object, options);

            // Act
            var result = await sut.DownloadContent(target);

            // Assert
            result.Should().BeSameAs(target);
            target.DownloadedUsing.Should().Be(DownloadedUsing.PRIMARY);
            target.TimeToDownload.Should().Be(elapsed);

            httpDownloaderMock.Verify(d => d.DownloadOnce("https://primary"), Times.Once);
            httpDownloaderMock.Verify(d => d.DownloadOnce("https://secondary"), Times.Never);

            fileServiceMock.Verify(fs => fs.ExportDownloadedFile(target, It.IsAny<Stream>()), Times.Once);
        }

        [Test]
        public async Task DownloadContent_WhenPrimaryLinkMissing_FallsBackToSecondary()
        {
            // Arrange
            var targetMock = CreateTarget(primary: "   ", secondary: "https://secondary");
            var target = targetMock.Object;

            using var stream = new MemoryStream(new byte[] { 9 });
            var elapsed = TimeSpan.FromMilliseconds(50);

            httpDownloaderMock
                .Setup(d => d.DownloadOnce("https://secondary"))
                .ReturnsAsync((stream, elapsed));

            var options = CreateOptions(downloadRetries: 3, secondsBetweenRetries: 0);
            var sut = new DownloadService(logger, fileServiceMock.Object, httpDownloaderMock.Object, options);

            // Act
            var result = await sut.DownloadContent(target);

            // Assert
            result.Should().BeSameAs(target);
            target.DownloadedUsing.Should().Be(DownloadedUsing.SECONDARY);
            target.TimeToDownload.Should().Be(elapsed);

            httpDownloaderMock.Verify(d => d.DownloadOnce(It.IsAny<string>()), Times.Once);
            httpDownloaderMock.Verify(d => d.DownloadOnce("https://secondary"), Times.Once);

            fileServiceMock.Verify(fs => fs.ExportDownloadedFile(target, It.IsAny<Stream>()), Times.Once);
        }

        [Test]
        public async Task DownloadContent_WhenPrimaryFailsAfterRetries_ThenSecondarySucceeds_UsesSecondary()
        {
            // Arrange
            var targetMock = CreateTarget(primary: "https://primary", secondary: "https://secondary");
            var target = targetMock.Object;

            httpDownloaderMock
                .Setup(d => d.DownloadOnce("https://primary"))
                .ThrowsAsync(new HttpRequestException("fail"));

            using var secondaryStream = new MemoryStream(new byte[] { 7, 7 });
            var secondaryElapsed = TimeSpan.FromMilliseconds(222);

            httpDownloaderMock
                .Setup(d => d.DownloadOnce("https://secondary"))
                .ReturnsAsync((secondaryStream, secondaryElapsed));

            var options = CreateOptions(downloadRetries: 3, secondsBetweenRetries: 0);
            var sut = new DownloadService(logger, fileServiceMock.Object, httpDownloaderMock.Object, options);

            // Act
            var result = await sut.DownloadContent(target);

            // Assert
            result.Should().BeSameAs(target);
            target.DownloadedUsing.Should().Be(DownloadedUsing.SECONDARY);
            target.TimeToDownload.Should().Be(secondaryElapsed);

            httpDownloaderMock.Verify(d => d.DownloadOnce("https://primary"), Times.Exactly(3));
            httpDownloaderMock.Verify(d => d.DownloadOnce("https://secondary"), Times.Once);

            fileServiceMock.Verify(fs => fs.ExportDownloadedFile(target, It.IsAny<Stream>()), Times.Once);
        }

        [Test]
        public async Task DownloadContent_WhenBothPrimaryAndSecondaryFail_SetsNoneAndDoesNotExport()
        {
            // Arrange
            var targetMock = CreateTarget(primary: "https://primary", secondary: "https://secondary");
            var target = targetMock.Object;

            httpDownloaderMock
                .Setup(d => d.DownloadOnce(It.IsAny<string>()))
                .ThrowsAsync(new HttpRequestException("fail"));

            var options = CreateOptions(downloadRetries: 2, secondsBetweenRetries: 0);
            var sut = new DownloadService(logger, fileServiceMock.Object, httpDownloaderMock.Object, options);

            // Act
            var result = await sut.DownloadContent(target);

            // Assert
            result.Should().BeSameAs(target);
            target.DownloadedUsing.Should().Be(DownloadedUsing.NONE);
            target.TimeToDownload.Should().BeNull();

            httpDownloaderMock.Verify(d => d.DownloadOnce("https://primary"), Times.Exactly(2));
            httpDownloaderMock.Verify(d => d.DownloadOnce("https://secondary"), Times.Exactly(2));

            fileServiceMock.Verify(fs => fs.ExportDownloadedFile(It.IsAny<IDownloadTarget>(), It.IsAny<Stream>()), Times.Never);
        }

        [Test]
        public void DownloadContent_WhenExportThrows_PropagatesException_AndDisposesDownloadedStream()
        {
            // Arrange
            var targetMock = CreateTarget(primary: "https://primary", secondary: "https://secondary");
            var target = targetMock.Object;

            var disposableStream = new TrackingStream(new byte[] { 1, 2, 3 });
            httpDownloaderMock
                .Setup(d => d.DownloadOnce("https://primary"))
                .ReturnsAsync((disposableStream, TimeSpan.FromMilliseconds(1)));

            fileServiceMock
                .Setup(fs => fs.ExportDownloadedFile(target, It.IsAny<Stream>()))
                .ThrowsAsync(new InvalidOperationException("export failed"));

            var options = CreateOptions(downloadRetries: 1, secondsBetweenRetries: 0);
            var sut = new DownloadService(logger, fileServiceMock.Object, httpDownloaderMock.Object, options);

            // Act
            Func<Task> act = async () => await sut.DownloadContent(target);

            // Assert
            act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("export failed");

            disposableStream.WasDisposed.Should().BeTrue("DownloadService wraps the stream in await using");
        }

        // ---------------- Helpers ----------------

        private static IOptions<DownloaderSettings> CreateOptions(int downloadRetries, int secondsBetweenRetries)
            => Options.Create(new DownloaderSettings
            {
                DownloadRetries = downloadRetries,
                SecondsWaitBetweenRetry = secondsBetweenRetries
            });

        private static Mock<IDownloadTarget> CreateTarget(string? primary, string? secondary)
        {
            var mock = new Mock<IDownloadTarget>();

            // BeginTargetScope(target) will likely read OutputFileName, so set it.
            mock.SetupGet(t => t.OutputFileName).Returns("TestFile");

            mock.SetupGet(t => t.PrimaryLink).Returns(primary);
            mock.SetupGet(t => t.SecondaryLink).Returns(secondary);

            // Service sets these
            mock.SetupProperty(t => t.DownloadedUsing, DownloadedUsing.NONE);
            mock.SetupProperty(t => t.TimeToDownload, (TimeSpan?)null);

            return mock;
        }

        private sealed class TrackingStream : MemoryStream
        {
            public bool WasDisposed { get; private set; }

            public TrackingStream(byte[] buffer) : base(buffer) { }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                WasDisposed = true;
            }
        }
    }
}