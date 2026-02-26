using Downloader.Service;
using FluentAssertions;
using Moq;
using Moq.Protected;
using System.Net;

namespace Downloader.Tests.Service
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Moq;
    using Moq.Protected;
    using NUnit.Framework;

    namespace Downloader.Tests.Service
    {
        [TestFixture]
        public class HttpFileDownloaderServiceTests
        {
            [Test]
            public async Task DownloadOnce_WhenResponseIsSuccess_ReturnsStreamWithSameBytes_AndPositionIsZero()
            {
                // Arrange
                var link = "https://example.com/file.bin";
                var payload = new byte[] { 1, 2, 3, 4, 5 };

                var handlerMock = CreateHandlerReturning(HttpStatusCode.OK, payload, out var capture);
                using var httpClient = new HttpClient(handlerMock.Object);
                var sut = new HttpFileDownloaderService(httpClient);

                // Act
                var (stream, elapsed) = await sut.DownloadOnce(link);

                // Assert
                capture.Request.Should().NotBeNull();
                capture.Request!.Method.Should().Be(HttpMethod.Get);
                capture.Request.RequestUri.Should().Be(new Uri(link));

                elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);

                stream.Position.Should().Be(0);

                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.ToArray().Should().Equal(payload);
            }

            [Test]
            public void DownloadOnce_WhenResponseIsNonSuccess_ThrowsHttpRequestException()
            {
                // Arrange
                var link = "https://example.com/missing";

                var handlerMock = CreateHandlerReturning(HttpStatusCode.NotFound, Array.Empty<byte>(), out _);
                using var httpClient = new HttpClient(handlerMock.Object);
                var sut = new HttpFileDownloaderService(httpClient);

                // Act
                Func<Task> act = async () => await sut.DownloadOnce(link);

                // Assert
                act.Should().ThrowAsync<HttpRequestException>();
            }

            [Test]
            public void DownloadOnce_WhenCancelled_ThrowsOperationCanceledException()
            {
                // Arrange
                var link = "https://example.com/file.bin";

                var handlerMock = new Mock<HttpMessageHandler>();
                handlerMock.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .Returns<HttpRequestMessage, CancellationToken>((_, ct) =>
                    {
                        ct.ThrowIfCancellationRequested();

                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(new byte[] { 1 })
                        });
                    });

                using var httpClient = new HttpClient(handlerMock.Object);
                var sut = new HttpFileDownloaderService(httpClient);

                using var cts = new CancellationTokenSource();
                cts.Cancel();

                // Act
                Func<Task> act = async () => await sut.DownloadOnce(link, cts.Token);

                // Assert
                act.Should().ThrowAsync<OperationCanceledException>();
            }

            private sealed class RequestCapture
            {
                public HttpRequestMessage? Request { get; set; }
            }

            private static Mock<HttpMessageHandler> CreateHandlerReturning(
                HttpStatusCode statusCode,
                byte[] contentBytes,
                out RequestCapture capture)
            {
                var captureLocal = new RequestCapture();
                var handlerMock = new Mock<HttpMessageHandler>();

                handlerMock.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                    {
                        captureLocal.Request = req;
                    })
                    .ReturnsAsync(() => new HttpResponseMessage(statusCode)
                    {
                        Content = new ByteArrayContent(contentBytes)
                    });

                capture = captureLocal;

                return handlerMock;
            }
        }
    }
}