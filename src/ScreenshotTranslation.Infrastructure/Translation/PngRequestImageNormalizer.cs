using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ScreenshotTranslation.Infrastructure.Translation;

internal sealed class PngRequestImageNormalizer : IRequestImageNormalizer
{
    public const int MaxLongEdgePixels = 2048;
    public const int MaxEncodedPngBytes = 8 * 1024 * 1024;

    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public Task<string> NormalizeToDataUrlAsync(
        ReadOnlyMemory<byte> pngBytes,
        CancellationToken cancellationToken) => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedPng = Normalize(pngBytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var base64 = Convert.ToBase64String(normalizedPng);
            cancellationToken.ThrowIfCancellationRequested();
            return $"data:image/png;base64,{base64}";
        }, cancellationToken);

    public static byte[] Normalize(ReadOnlyMemory<byte> pngBytes) =>
        Normalize(pngBytes, CancellationToken.None);

    private static byte[] Normalize(
        ReadOnlyMemory<byte> pngBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (pngBytes.IsEmpty ||
            pngBytes.Length < PngSignature.Length ||
            !pngBytes.Span[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            throw new ArgumentException("Screenshot content must be a valid PNG.", nameof(pngBytes));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var input = pngBytes.ToArray();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new MemoryStream(input, writable: false);
            using var source = new Bitmap(stream);
            cancellationToken.ThrowIfCancellationRequested();
            if (source.RawFormat.Guid != ImageFormat.Png.Guid)
            {
                throw new ArgumentException("Screenshot content must be a valid PNG.", nameof(pngBytes));
            }

            var longEdge = Math.Max(source.Width, source.Height);
            if (longEdge <= MaxLongEdgePixels && input.Length <= MaxEncodedPngBytes)
            {
                return input;
            }

            var edgeScale = Math.Min(1d, MaxLongEdgePixels / (double)longEdge);
            var payloadScale = input.Length > MaxEncodedPngBytes
                ? Math.Min(0.95d, Math.Sqrt(MaxEncodedPngBytes / (double)input.Length) * 0.95d)
                : 1d;
            var scale = Math.Min(edgeScale, payloadScale);
            var width = Math.Max(1, (int)Math.Floor(source.Width * scale));
            var height = Math.Max(1, (int)Math.Floor(source.Height * scale));
            EnsureReduced(source.Width, source.Height, ref width, ref height);

            cancellationToken.ThrowIfCancellationRequested();
            var output = ResizeAsPng(source, width, height, cancellationToken);
            while (output.Length > MaxEncodedPngBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (width == 1 && height == 1)
                {
                    throw new InvalidOperationException("The PNG payload could not be reduced below the request limit.");
                }

                var nextScale = Math.Min(
                    0.90d,
                    Math.Sqrt(MaxEncodedPngBytes / (double)output.Length) * 0.95d);
                var nextWidth = Math.Max(1, (int)Math.Floor(width * nextScale));
                var nextHeight = Math.Max(1, (int)Math.Floor(height * nextScale));
                EnsureReduced(width, height, ref nextWidth, ref nextHeight);
                width = nextWidth;
                height = nextHeight;
                cancellationToken.ThrowIfCancellationRequested();
                output = ResizeAsPng(source, width, height, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return output;
        }
        catch (ArgumentException exception) when (exception.ParamName != nameof(pngBytes))
        {
            throw new ArgumentException("Screenshot content must be a valid PNG.", nameof(pngBytes), exception);
        }
    }

    private static byte[] ResizeAsPng(
        Bitmap source,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var resized = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(resized))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            cancellationToken.ThrowIfCancellationRequested();
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, width, height),
                0,
                0,
                source.Width,
                source.Height,
                GraphicsUnit.Pixel);
            cancellationToken.ThrowIfCancellationRequested();
        }

        using var stream = new MemoryStream();
        cancellationToken.ThrowIfCancellationRequested();
        resized.Save(stream, ImageFormat.Png);
        cancellationToken.ThrowIfCancellationRequested();
        return stream.ToArray();
    }

    private static void EnsureReduced(
        int currentWidth,
        int currentHeight,
        ref int nextWidth,
        ref int nextHeight)
    {
        if (nextWidth < currentWidth || nextHeight < currentHeight)
        {
            return;
        }

        if (currentWidth >= currentHeight && currentWidth > 1)
        {
            nextWidth = currentWidth - 1;
            nextHeight = Math.Max(1, (int)Math.Round(
                currentHeight * (nextWidth / (double)currentWidth),
                MidpointRounding.AwayFromZero));
        }
        else if (currentHeight > 1)
        {
            nextHeight = currentHeight - 1;
            nextWidth = Math.Max(1, (int)Math.Round(
                currentWidth * (nextHeight / (double)currentHeight),
                MidpointRounding.AwayFromZero));
        }
    }
}
