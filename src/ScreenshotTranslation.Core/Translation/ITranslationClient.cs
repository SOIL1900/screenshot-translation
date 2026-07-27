using ScreenshotTranslation.Core.Configuration;

namespace ScreenshotTranslation.Core.Translation;

public interface ITranslationClient
{
    Task<ScreenshotTranslationResult> TranslateScreenshotAsync(
        ReadOnlyMemory<byte> pngBytes,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken);

    Task<ReplyTranslationResult> TranslateReplyAsync(
        string input,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken);

    Task TestConnectionAsync(ModelSettings settings, CancellationToken cancellationToken);
}
