namespace ScreenshotTranslation.Core.Translation;

public enum TranslationResultStatus
{
    Ok,
    NoText
}

public enum TranslationErrorCode
{
    Unauthorized,
    RateLimited,
    Timeout,
    ServiceUnavailable,
    InvalidResponse,
    Network
}

public sealed record ScreenshotTranslationResult(
    TranslationResultStatus Status,
    string SourceLanguage,
    string SourceLanguageCode,
    string Translation);

public sealed record ReplyTranslationResult(string TargetLanguageCode, string Translation);

public sealed class TranslationClientException(
    TranslationErrorCode code,
    string message,
    Exception? inner = null) : Exception(message, inner)
{
    public TranslationErrorCode Code { get; } = code;
}
