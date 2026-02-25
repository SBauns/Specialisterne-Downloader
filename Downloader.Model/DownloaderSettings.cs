namespace Downloader.Model
{
    public class DownloaderSettings
    {
        /// <summary>
        /// Full path to location where generated reports will be placed.
        /// </summary>
        public string ReportsOutputPath { get; set; }

        /// <summary>
        /// Full path to location where downloaded files will be placed.
        /// </summary>
        public string DownloadedFilesOutputPath { get; set; }

        /// <summary>
        /// Full path to Excel file containing the links to be downloaded / checked.
        /// Expects the following columns to contain...
        /// <list type="bullet">
        /// <item><description>A: Name of output downloaded file.</description></item>
        /// <item><description>AL: Primary download Link.</description></item>
        /// <item><description>AM: Secondary download Link.</description></item>
        /// </list>
        /// </summary>
        public string FilesToDownloadExcelInput { get; set; }

        /// <summary>
        /// Maximum amount of created threads, to split download workload on.
        /// Suggested value: 3 - 10
        /// </summary>
        public int MaxConcurrentDownloads { get; set; } = 5;

        /// <summary>
        /// Maximum retries using Primary or Secondary link, before marking link dead.
        /// Suggested value: 1 - 5
        /// </summary>
        public int DownloadRetries { get; set; } = 3;

        /// <summary>
        /// Seconds waited between download retries.
        /// Suggested value: 1 - 15
        /// </summary>
        public int SecondsWaitBetweenRetry { get; set; } = 5;

        /// <summary>
        /// Defines the lower inclusive index bound of the targets to generate.
        /// 
        /// A value of -1 means no lower limit (start from the first target).
        /// 
        /// Example:
        ///     0  -> start from first target
        ///     10 -> start from the 11th target
        ///     -1 -> no lower bound
        /// </summary>
        public int TargetStartIndex { get; set; } = -1;

        /// <summary>
        /// Defines the upper inclusive index bound of the targets to generate.
        /// 
        /// A value of -1 means no upper limit (include all remaining targets).
        /// 
        /// Example:
        ///     99 -> stop at the 100th target
        ///     -1 -> no upper bound
        /// </summary>
        public int TargetEndIndex { get; set; } = -1;
    }
}