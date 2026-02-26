namespace Downloader.Abstraction.Interfaces.Services
{
    public interface IHttpFileDownloaderService
    {
        Task<(Stream Stream, TimeSpan Elapsed)> DownloadOnce(string link, CancellationToken ct = default);
    }
}