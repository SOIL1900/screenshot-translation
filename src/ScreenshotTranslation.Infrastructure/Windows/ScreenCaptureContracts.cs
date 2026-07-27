using ScreenshotTranslation.Core.Geometry;

namespace ScreenshotTranslation.Infrastructure.Windows;

public sealed record MonitorBounds(
    nint Handle,
    PixelRect PhysicalBounds,
    PixelRect PhysicalWorkArea)
{
    public PixelRect FrameLocalWorkArea => new(
        checked(PhysicalWorkArea.X - PhysicalBounds.X),
        checked(PhysicalWorkArea.Y - PhysicalBounds.Y),
        PhysicalWorkArea.Width,
        PhysicalWorkArea.Height);
}

public sealed record CapturedMonitorFrame(MonitorBounds Monitor, byte[] PngBytes);

public interface IMonitorService
{
    MonitorBounds GetMonitorUnderCursor();
}

public interface IScreenCaptureService
{
    CapturedMonitorFrame Capture(MonitorBounds monitor);
}

public interface IPngCropService
{
    byte[] Crop(byte[] capturedPng, PixelRect cropRectangle);
}
