using System.Text.Json;
using ScreenshotTranslation.Core.Abstractions;
using ScreenshotTranslation.Core.Configuration;

namespace ScreenshotTranslation.Infrastructure.Configuration;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _baseDirectory;
    private readonly TimeProvider _timeProvider;

    public JsonSettingsStore(string baseDirectory, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _baseDirectory = baseDirectory;
        _timeProvider = timeProvider;
        SettingsPath = Path.Combine(baseDirectory, "settings.json");
    }

    public string SettingsPath { get; }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SettingsPath))
        {
            return AppSettings.CreateDefault();
        }

        try
        {
            await using var stream = new FileStream(
                SettingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (settings is null)
            {
                throw new JsonException("Settings JSON cannot be null.");
            }

            if (SettingsValidator.ValidatePersisted(settings).Count > 0)
            {
                throw new JsonException("Settings JSON violates persisted settings invariants.");
            }

            return settings;
        }
        catch (JsonException)
        {
            MoveCorruptSettings();
            return AppSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(_baseDirectory);
        var temporaryPath = SettingsPath + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(SettingsPath))
            {
                File.Replace(temporaryPath, SettingsPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, SettingsPath);
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private void MoveCorruptSettings()
    {
        var timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmssfff");
        var backupPath = Path.Combine(_baseDirectory, $"settings.corrupt-{timestamp}.json");
        File.Move(SettingsPath, backupPath);
    }
}
