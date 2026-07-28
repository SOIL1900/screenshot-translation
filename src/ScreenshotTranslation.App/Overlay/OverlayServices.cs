using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Translation;

namespace ScreenshotTranslation.App.Overlay;

public interface IOverlayTranslationCoordinator
{
    Task<ScreenshotTranslationResult?> TranslateScreenshotAsync(
        ReadOnlyMemory<byte> pngBytes,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken);

    Task<ReplyTranslationResult?> TranslateReplyAsync(
        string input,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken);

    void Cancel();
}

public sealed class OverlayTranslationCoordinator(TranslationCoordinator coordinator)
    : IOverlayTranslationCoordinator
{
    public Task<ScreenshotTranslationResult?> TranslateScreenshotAsync(
        ReadOnlyMemory<byte> pngBytes,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken) =>
        coordinator.TranslateScreenshotAsync(pngBytes, targetLanguageCode, settings, cancellationToken);

    public Task<ReplyTranslationResult?> TranslateReplyAsync(
        string input,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken) =>
        coordinator.TranslateReplyAsync(input, targetLanguageCode, settings, cancellationToken);

    public void Cancel() => coordinator.Cancel();
}

public interface IOverlayClipboardService
{
    void SetText(string text);
}

public sealed class WpfOverlayClipboardService : IOverlayClipboardService
{
    public void SetText(string text) => System.Windows.Clipboard.SetText(text);
}

public interface IOverlayDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class SystemOverlayDelay : IOverlayDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}
