using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenshotTranslation.Infrastructure.Tests.Translation;

internal static class TestPngFactory
{
    public static byte[] CreateSolid(int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.FromArgb(255, 62, 34, 112));
        }

        return Encode(bitmap);
    }

    public static byte[] CreateHighEntropy(int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rectangle = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[checked(Math.Abs(data.Stride) * height)];
            new Random(20260728).NextBytes(bytes);
            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return Encode(bitmap);
    }

    public static (int Width, int Height, Guid Format) Inspect(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes, writable: false);
        using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        return (image.Width, image.Height, image.RawFormat.Guid);
    }

    private static byte[] Encode(Image image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
