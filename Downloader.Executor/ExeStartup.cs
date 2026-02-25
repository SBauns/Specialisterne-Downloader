using System.Text.Json;
using System.Text.Json.Nodes;
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

        public string GetApplicationDataPath()
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

            EnsureAndNormalizeAppSettings(appSettingsPath);

            configurationBuilder.AddJsonFile(appSettingsPath, false, true);
        }

        private void EnsureAndNormalizeAppSettings(string appSettingsPath)
        {
            var directory = Path.GetDirectoryName(appSettingsPath) ??
                            throw new InvalidOperationException(
                                $"Could not determine directory for '{appSettingsPath}'.");

            Directory.CreateDirectory(directory);

            var rootObj = LoadOrCreateRootObject(appSettingsPath, out var createdNewFile);

            var changed = createdNewFile;

            var downloaderObj = EnsureDownloaderSection(rootObj, ref changed);
            changed |= NormalizeDownloaderBounds(downloaderObj);

            if (changed)
                WriteAppSettings(appSettingsPath, rootObj);
        }

        private JsonObject LoadOrCreateRootObject(string appSettingsPath, out bool createdNewFile)
        {
            createdNewFile = false;

            if (!File.Exists(appSettingsPath))
            {
                createdNewFile = true;
                return CreateDefaultRootObject();
            }

            try
            {
                var jsonText = File.ReadAllText(appSettingsPath);
                var node = JsonNode.Parse(jsonText);

                if (node is JsonObject obj)
                    return obj;

                // If root isn't an object, treat it as corrupt and recreate
                createdNewFile = true;
                return CreateDefaultRootObject();
            }
            catch
            {
                // If parsing fails, fall back to default (fail-safe)
                createdNewFile = true;
                return CreateDefaultRootObject();
            }
        }

        private JsonObject CreateDefaultRootObject()
        {
            return new JsonObject
            {
                ["Downloader"] = JsonSerializer.SerializeToNode(defaultDownloaderSettings)
            };
        }

        private JsonObject EnsureDownloaderSection(JsonObject rootObj, ref bool changed)
        {
            if (rootObj["Downloader"] is JsonObject downloaderObj)
                return downloaderObj;

            downloaderObj = JsonSerializer.SerializeToNode(defaultDownloaderSettings) as JsonObject
                            ?? new JsonObject();

            rootObj["Downloader"] = downloaderObj;
            changed = true;

            return downloaderObj;
        }

        private bool NormalizeDownloaderBounds(JsonObject downloaderObj)
        {
            var changed = false;

            changed |= NormalizeIntMinMinusOne(
                downloaderObj,
                nameof(DownloaderSettings.TargetStartIndex));

            changed |= NormalizeIntMinMinusOne(
                downloaderObj,
                nameof(DownloaderSettings.TargetEndIndex));

            return changed;
        }

        private bool NormalizeIntMinMinusOne(JsonObject section, string propertyName)
        {
            if (section[propertyName] is null)
                return false;

            if (section[propertyName] is not JsonValue val)
                return false;

            if (!val.TryGetValue<int>(out var current))
                return false;

            if (current >= -1)
                return false;

            section[propertyName] = -1;
            return true;
        }

        private void WriteAppSettings(string appSettingsPath, JsonObject rootObj)
        {
            var json = rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

            var tmpPath = appSettingsPath + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Copy(tmpPath, appSettingsPath, overwrite: true);
            File.Delete(tmpPath);
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

        private bool ValidateIntegerSettings(DownloaderSettings settings)
        {
            // Normalize target slicing bounds (soft validation)
            if (settings.TargetStartIndex < -1)
                settings.TargetStartIndex = -1;

            if (settings.TargetEndIndex < -1)
                settings.TargetEndIndex = -1;

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

        private bool ValidateStringSettings(DownloaderSettings settings)
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