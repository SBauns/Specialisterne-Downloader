namespace Downloader.Abstraction.Interfaces.Model
{
    public interface IDownloadTarget
    {
        string OutputFileName { get; set; }
        string? PrimaryLink { get; set; }
        string? SecondaryLink { get; set; }
        bool WasSuccessfullyDownloaded { get; set; }
        TimeSpan? TimeToDownload { get; set; }
    }
}