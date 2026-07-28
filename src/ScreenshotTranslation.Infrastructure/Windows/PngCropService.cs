using System.Drawing;
using System.Drawing.Imaging;
using ScreenshotTranslation.Core.Geometry;

namespace ScreenshotTranslation.Infrastructure.Windows;

public sealed class PngCropService : IPngCropService
{
    public byte[] Crop(byte[] capturedPng, PixelRect cropRectangle)
    {
        ArgumentNullException.ThrowIfNull(capturedPng);
        if (capturedPng.Length == 0)
        {
            throw new ArgumentException("The captured PNG cannot be empty.", nameof(capturedPng));
        }

        using var sourceStream = new MemoryStream(capturedPng, writable: false);
        using var source = new Bitmap(sourceStream);
        if (source.RawFormat.Guid != ImageFormat.Png.Guid)
        {
            throw new ArgumentException("The captured image must be encoded as PNG.", nameof(capturedPng));
        }

        ValidateCropRectangle(cropRectangle, source.Width, source.Height);

        var sourceRectangle = new Rectangle(
            cropRectangle.X,
            cropRectangle.Y,
            cropRectangle.Width,
            cropRectangle.Height);
        using var cropped = source.Clone(sourceRectangle, PixelFormat.Format32bppArgb);
        using var outputStream = new MemoryStream();
        cropped.Save(outputStream, ImageFormat.Png);
        return outputStream.ToArray();
    }

    private static void ValidateCropRectangle(PixelRect rectangle, int frameWidth, int frameHeight)
    {
        var right = (long)rectangle.X + rectangle.Width;
        var bottom = (long)rectangle.Y + rectangle.Height;
        if (rectangle.X < 0 ||
            rectangle.Y < 0 ||
            rectangle.Width <= 0 ||
            rectangle.Height <= 0 ||
            right > frameWidth ||
            bottom > frameHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rectangle),
                "The crop rectangle must be fully contained within the captured frame.");
        }
    }
}
