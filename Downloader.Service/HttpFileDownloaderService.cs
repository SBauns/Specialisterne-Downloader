using System.Diagnostics;
using Downloader.Abstraction.Interfaces.Services;

namespace Downloader.Service
{
    public class HttpFileDownloaderService : IHttpFileDownloaderService
    {
        private readonly HttpClient httpClient;

        public HttpFileDownloaderService(HttpClient httpClient) => this.httpClient = httpClient;

        public async Task<(Stream Stream, TimeSpan Elapsed)> DownloadOnce(string link, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            using var response = await httpClient.GetAsync(link, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            // Buffer fully so the timing reflects the full download of the attempt.
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);

            sw.Stop();

            var ms = new MemoryStream(bytes, writable: false);
            ms.Position = 0;

            return (ms, sw.Elapsed);
        }
    }
}