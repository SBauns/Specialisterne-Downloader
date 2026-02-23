using Downloader.Abstraction.Interfaces.Model;

namespace Downloader.Model
{
    public class DownloadTarget : IDownloadTarget
    {
        /// <inheritdoc />
        public string OutputFileName { get; set; }

        /// <inheritdoc />
        public string PrimaryLink { get; set; }

        /// <inheritdoc />
        public string SecondaryLink { get; set; }

        /// <inheritdoc />
        public bool WasSuccessfullyDownloaded { get; set; }
    }
}