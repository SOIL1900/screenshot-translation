using System.Text.Json;
using ScreenshotTranslation.Core.Abstractions;

namespace ScreenshotTranslation.Infrastructure.Diagnostics;

public sealed class FileDiagnosticLog : IDiagnosticLog
{
    private const string UnrecognizedEventName = "unrecognized_event";

    private static readonly HashSet<string> AllowedEventNames = new(StringComparer.Ordinal)
    {
        "translation_failed",
        "hotkey_registration_failed",
        "startup_failed",
        "capture_workflow_failed"
    };

    private static readonly HashSet<string> AllowedCaptureStages = new(StringComparer.Ordinal)
    {
        "load_settings",
        "validate_settings",
        "capture_foreground_window",
        "locate_monitor",
        "capture_screen",
        "create_overlay",
        "show_overlay"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _baseDirectory;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileDiagnosticLog(string baseDirectory, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _baseDirectory = baseDirectory;
        _timeProvider = timeProvider;
        LogPath = Path.Combine(baseDirectory, "diagnostics.jsonl");
    }

    public string LogPath { get; }

    public async Task WriteAsync(
        string eventName,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new
        {
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            EventName = AllowedEventNames.Contains(eventName) ? eventName : UnrecognizedEventName,
            ExceptionType = exception?.GetType().FullName,
            HResult = exception?.HResult,
            CaptureStage = GetCaptureStage(exception)
        };
        var line = JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_baseDirectory);
            await File.AppendAllTextAsync(LogPath, line, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static string? GetCaptureStage(Exception? exception) =>
        exception?.Data["CaptureStage"] is string stage && AllowedCaptureStages.Contains(stage)
            ? stage
            : null;
}
