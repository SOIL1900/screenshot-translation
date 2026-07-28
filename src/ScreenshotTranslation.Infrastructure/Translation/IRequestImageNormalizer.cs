namespace ScreenshotTranslation.Infrastructure.Translation;

internal interface IRequestImageNormalizer
{
    Task<string> NormalizeToDataUrlAsync(
        ReadOnlyMemory<byte> pngBytes,
        CancellationToken cancellationToken);
}
