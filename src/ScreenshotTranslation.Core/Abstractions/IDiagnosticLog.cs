namespace ScreenshotTranslation.Core.Abstractions;

public interface IDiagnosticLog
{
    Task WriteAsync(
        string eventName,
        Exception? exception = null,
        CancellationToken cancellationToken = default);
}
