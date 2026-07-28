using ScreenshotTranslation.Core.Geometry;
using ScreenshotTranslation.Infrastructure.Windows;

namespace ScreenshotTranslation.Infrastructure.Tests.Windows;

public sealed class MonitorBoundsTests
{
    [Fact]
    public void Work_area_is_projected_to_the_negative_origin_monitors_frame()
    {
        var monitor = new MonitorBounds(
            (nint)2,
            new PixelRect(-1920, 100, 1920, 1080),
            new PixelRect(-1920, 140, 1920, 1040));

        Assert.Equal(new PixelRect(0, 40, 1920, 1040), monitor.FrameLocalWorkArea);
        Assert.Equal(new PixelRect(-1920, 100, 1920, 1080), monitor.PhysicalBounds);
    }
}
