using System.Text.Json;
using Downloader.Abstraction.Interfaces.Services;
using Downloader.Executor.Startup;
using Downloader.Executor.Startup.Modules;
using Downloader.Model;
using Downloader.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
// Reduces readability in multiple location in this file, if done.
// ReSharper disable ConvertIfStatementToReturnStatement

namespace Downloader.Executor
{
    public class ExeStartup : ModularStartup
    {
        private const string SETTINGS_SECTIONS = "Downloader";
        private const string SHARED_ROOT_FOLDER_NAME = "FangSoftware";
        private const string APP_FOLDER_NAME = "FileDownloader";
        private readonly DownloaderSettings defaultDownloaderSettings;

        public ExeStartup()
        {
            AddModule(new LoggingStartupModule(GetApplicationDataPath()));

            defaultDownloaderSettings = new DownloaderSettings()
            {
                DownloadedFilesOutputPath = Path.Combine(GetApplicationDataPath(), "Downloads"),
                ReportsOutputPath = Path.Combine(GetApplicationDataPath(), "Reports"),
                FilesToDownloadExcelInput = Path.Combine(GetApplicationDataPath(), "GRI_2017_2020.xlsx"),
            };
        }

        private string GetApplicationDataPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SHARED_ROOT_FOLDER_NAME, APP_FOLDER_NAME);
        }

        public IHost BuildHost(string[] args)
        {
            return Host.CreateDefaultBuilder(args).ConfigureAppConfiguration(ConfigureAppConfiguration)
                .ConfigureServices(SetupServices).Build();
        }

        private void ConfigureAppConfiguration(HostBuilderContext hostBuilderContext, IConfigurationBuilder configurationBuilder)
        {
            var appSettingsPath = Path.Combine(GetApplicationDataPath(), "appsettings.json");

            EnsureAppSettingsExists(appSettingsPath);

            configurationBuilder.AddJsonFile(appSettingsPath, false, true);
        }

        private void EnsureAppSettingsExists(string appSettingsPath)
        {
            if (File.Exists(appSettingsPath))
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(appSettingsPath) ??
                                throw new InvalidOperationException(
                                    $"Could not determine directory for '{appSettingsPath}'.");

                Directory.CreateDirectory(directory);

                var appSettingsObject = new
                {
                    Downloader = defaultDownloaderSettings
                };

                var json = JsonSerializer.Serialize(appSettingsObject,
                    new JsonSerializerOptions {WriteIndented = true});

                using var stream =
                    new FileStream(appSettingsPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream);
                writer.Write(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Missing '{appSettingsPath}' and failed to generate a default one. " +
                    $"Fix permissions / path and re-run.", ex);
            }
        }


        /// <inheritdoc />
        public override void ConfigureServices(HostBuilderContext hostBuilderContext, IServiceCollection services)
        {
            base.ConfigureServices(hostBuilderContext, services);

            services.AddOptions<DownloaderSettings>().Bind(
                hostBuilderContext.Configuration.GetSection(SETTINGS_SECTIONS))
                .Validate(ValidateDownloaderSettings).ValidateOnStart();

            services.AddScoped<IOrchestratorService, OrchestratorService>();
            services.AddScoped<IDownloadService, DownloadService>();
            services.AddScoped<IFileService, LocalDriveFileService>();
            services.AddScoped<IReportService, MarkdownReportService>();
            services.AddScoped<IInputReaderService, ExcelInputReaderService>();
        }

        private bool ValidateDownloaderSettings(DownloaderSettings? settings)
        {
            if (settings is null)
                return false;

            if (!ValidateStringSettings(settings)) 
                return false;

            if (!ValidateIntegerSettings(settings))
                return false;

            return true;
        }

        private static bool ValidateIntegerSettings(DownloaderSettings settings)
        {
            // Must be at least 1 — otherwise nothing downloads
            if (settings.MaxConcurrentDownloads < 1)
                return false;

            // Retries must not be negative
            if (settings.DownloadRetries < 0)
                return false;

            // Waiting time must not be negative
            if (settings.SecondsWaitBetweenRetry < 0)
                return false;

            return true;
        }

        private static bool ValidateStringSettings(DownloaderSettings settings)
        {
            // ---- Required string properties ----
            if (string.IsNullOrWhiteSpace(settings.DownloadedFilesOutputPath))
                return false;

            if (string.IsNullOrWhiteSpace(settings.ReportsOutputPath))
                return false;

            if (string.IsNullOrWhiteSpace(settings.FilesToDownloadExcelInput))
                return false;

            // ---- Ensure paths are absolute ----
            if (!Path.IsPathRooted(settings.DownloadedFilesOutputPath))
                return false;

            if (!Path.IsPathRooted(settings.ReportsOutputPath))
                return false;

            if (!Path.IsPathRooted(settings.FilesToDownloadExcelInput))
                return false;
            return true;
        }
    }
}