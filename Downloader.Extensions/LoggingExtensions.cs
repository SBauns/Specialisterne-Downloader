using Downloader.Abstraction.Interfaces.Model;
using Microsoft.Extensions.Logging;

namespace Downloader.Extensions
{
    public static class LoggingExtensions
    {
        public static IDisposable BeginTargetScope(this ILogger logger, IDownloadTarget target)
        {
            if (target is null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(target.OutputFileName))
                throw new ArgumentException("OutputFileName must be set.", nameof(target));

            return logger.BeginScope("Download: {Download}", target.OutputFileName) ?? NullScope.Instance;
        }

        public static IDisposable BeginTargetScope(this ILogger logger, string outputFileName)
        {
            if (string.IsNullOrWhiteSpace(outputFileName))
                throw new ArgumentException("OutputFileName must be set.", nameof(outputFileName));

            return logger.BeginScope("Download: {Download}", outputFileName) ?? NullScope.Instance;
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            private NullScope()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}