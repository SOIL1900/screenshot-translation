using ScreenshotTranslation.App.Overlay;
using ScreenshotTranslation.Core.Geometry;

namespace ScreenshotTranslation.App.Tests.Overlay;

public sealed class OverlayPanelLayoutTests
{
    [Fact]
    public void Small_high_dpi_work_area_constrains_panel_size_and_physical_placement()
    {
        var mapper = new OverlayCoordinateMapper(144, 144);
        var workArea = new PixelRect(160, 90, 500, 420);
        var selection = new PixelRect(210, 160, 320, 120);

        var layout = OverlayPanelLayout.Calculate(selection, workArea, mapper);

        Assert.True(layout.WidthDip < OverlayViewModel.PanelMinimumWidth);
        Assert.True(layout.HeightDip < OverlayViewModel.PanelHeight);
        Assert.Equal(workArea.Width, layout.PhysicalBounds.Width);
        Assert.Equal(workArea.Height, layout.PhysicalBounds.Height);
        Assert.True(layout.PhysicalBounds.Left >= workArea.Left);
        Assert.True(layout.PhysicalBounds.Top >= workArea.Top);
        Assert.True(layout.PhysicalBounds.Right <= workArea.Right);
        Assert.True(layout.PhysicalBounds.Bottom <= workArea.Bottom);
    }
}
