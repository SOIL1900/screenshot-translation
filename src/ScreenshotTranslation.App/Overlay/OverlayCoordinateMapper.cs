using System.Runtime.InteropServices;
using ScreenshotTranslation.Core.Geometry;

namespace ScreenshotTranslation.App.Overlay;

public sealed class OverlayCoordinateMapper
{
    private const double DefaultDpi = 96d;
    private readonly double _scaleX;
    private readonly double _scaleY;

    public OverlayCoordinateMapper(uint dpiX, uint dpiY)
    {
        ArgumentOutOfRangeException.ThrowIfZero(dpiX);
        ArgumentOutOfRangeException.ThrowIfZero(dpiY);
        _scaleX = dpiX / DefaultDpi;
        _scaleY = dpiY / DefaultDpi;
    }

    public static OverlayCoordinateMapper FromWindow(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            throw new ArgumentException("A valid overlay window handle is required.", nameof(windowHandle));
        }

        var dpi = GetDpiForWindow(windowHandle);
        return new OverlayCoordinateMapper(
            dpi == 0 ? 96u : dpi,
            dpi == 0 ? 96u : dpi);
    }

    public PixelPoint ToPhysical(System.Windows.Point point) => new(
        DipLengthToPhysicalX(point.X),
        DipLengthToPhysicalY(point.Y));

    public System.Windows.Rect ToDip(PixelRect rectangle) => new(
        rectangle.X / _scaleX,
        rectangle.Y / _scaleY,
        rectangle.Width / _scaleX,
        rectangle.Height / _scaleY);

    public int DipLengthToPhysicalX(double dipLength) =>
        checked((int)Math.Round(dipLength * _scaleX, MidpointRounding.AwayFromZero));

    public int DipLengthToPhysicalY(double dipLength) =>
        checked((int)Math.Round(dipLength * _scaleY, MidpointRounding.AwayFromZero));

    public double PhysicalLengthToDipX(double physicalLength) => physicalLength / _scaleX;

    public double PhysicalLengthToDipY(double physicalLength) => physicalLength / _scaleY;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);
}
