using System.Drawing;
using System.Drawing.Imaging;
using ScreenshotTranslation.Core.Geometry;
using ScreenshotTranslation.Infrastructure.Windows;

namespace ScreenshotTranslation.Infrastructure.Tests.Windows;

public sealed class PngCropServiceTests
{
    [Fact]
    public void Crop_returns_requested_pixels_without_resampling()
    {
        var sourcePng = CreateQuadrantPng();
        var service = new PngCropService();

        var croppedPng = service.Crop(sourcePng, new PixelRect(50, 0, 50, 50));

        using var stream = new MemoryStream(croppedPng);
        using var cropped = new Bitmap(stream);
        Assert.Equal(50, cropped.Width);
        Assert.Equal(50, cropped.Height);
        Assert.Equal(Color.Blue.ToArgb(), cropped.GetPixel(25, 25).ToArgb());
    }

    [Fact]
    public void Crop_rejects_rectangle_outside_captured_frame()
    {
        var sourcePng = CreateQuadrantPng();
        var service = new PngCropService();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => service.Crop(sourcePng, new PixelRect(75, 75, 50, 50)));
    }

    private static byte[] CreateQuadrantPng()
    {
        using var bitmap = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        using var red = new SolidBrush(Color.Red);
        using var blue = new SolidBrush(Color.Blue);
        using var green = new SolidBrush(Color.Green);
        using var yellow = new SolidBrush(Color.Yellow);

        graphics.FillRectangle(red, 0, 0, 50, 50);
        graphics.FillRectangle(blue, 50, 0, 50, 50);
        graphics.FillRectangle(green, 0, 50, 50, 50);
        graphics.FillRectangle(yellow, 50, 50, 50, 50);

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
