using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ScreenshotTranslation.Infrastructure.Translation;

internal static class PngRequestImageNormalizer
{
    public const int MaxLongEdgePixels = 2048;
    public const int MaxEncodedPngBytes = 8 * 1024 * 1024;

    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] Normalize(ReadOnlyMemory<byte> pngBytes)
    {
        if (pngBytes.IsEmpty ||
            pngBytes.Length < PngSignature.Length ||
            !pngBytes.Span[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            throw new ArgumentException("Screenshot content must be a valid PNG.", nameof(pngBytes));
        }

        var input = pngBytes.ToArray();
        try
        {
            using var stream = new MemoryStream(input, writable: false);
            using var source = new Bitmap(stream);
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

            var output = ResizeAsPng(source, width, height);
            while (output.Length > MaxEncodedPngBytes)
            {
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
                output = ResizeAsPng(source, width, height);
            }

            return output;
        }
        catch (ArgumentException exception) when (exception.ParamName != nameof(pngBytes))
        {
            throw new ArgumentException("Screenshot content must be a valid PNG.", nameof(pngBytes), exception);
        }
    }

    private static byte[] ResizeAsPng(Bitmap source, int width, int height)
    {
        using var resized = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(resized))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, width, height),
                0,
                0,
                source.Width,
                source.Height,
                GraphicsUnit.Pixel);
        }

        using var stream = new MemoryStream();
        resized.Save(stream, ImageFormat.Png);
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
