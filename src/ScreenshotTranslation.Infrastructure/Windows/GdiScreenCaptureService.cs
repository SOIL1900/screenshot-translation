using System.Drawing;
using System.Drawing.Imaging;

namespace ScreenshotTranslation.Infrastructure.Windows;

public sealed class GdiScreenCaptureService : IScreenCaptureService
{
    public CapturedMonitorFrame Capture(MonitorBounds monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);

        var bounds = monitor.PhysicalBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monitor), "Monitor dimensions must be positive.");
        }

        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                bounds.X,
                bounds.Y,
                0,
                0,
                new Size(bounds.Width, bounds.Height),
                CopyPixelOperation.SourceCopy);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return new CapturedMonitorFrame(monitor, stream.ToArray());
    }
}
