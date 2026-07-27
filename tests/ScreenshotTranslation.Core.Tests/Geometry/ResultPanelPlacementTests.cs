using ScreenshotTranslation.Core.Geometry;

namespace ScreenshotTranslation.Core.Tests.Geometry;

public sealed class ResultPanelPlacementTests
{
    [Fact]
    public void Panel_is_placed_below_when_there_is_enough_room()
    {
        var screen = new PixelRect(0, 0, 1920, 1080);
        var selection = new PixelRect(100, 100, 300, 150);

        var result = ResultPanelPlacement.Place(selection, 520, 260, screen, 12);

        Assert.Equal(new PixelRect(100, 262, 520, 260), result);
    }

    [Fact]
    public void Panel_flips_above_and_stays_inside_the_screen()
    {
        var screen = new PixelRect(0, 0, 1920, 1080);
        var selection = new PixelRect(1600, 900, 300, 150);

        var result = ResultPanelPlacement.Place(selection, 520, 260, screen, 12);

        Assert.Equal(selection.Top - 12, result.Bottom);
        Assert.True(result.Right <= screen.Right);
        Assert.Equal(screen.Right, result.Right);
    }

    [Fact]
    public void Panel_clamps_to_a_screen_with_nonzero_origin()
    {
        var screen = new PixelRect(-1920, -200, 1920, 1080);
        var selection = new PixelRect(-100, 700, 100, 100);

        var result = ResultPanelPlacement.Place(selection, 520, 260, screen, 12);

        Assert.Equal(screen.Right, result.Right);
        Assert.Equal(selection.Top - 12, result.Bottom);
        Assert.True(result.Top >= screen.Top);
        Assert.True(result.Right <= screen.Right);
    }

    [Fact]
    public void Panel_clamps_vertically_when_neither_side_has_enough_room()
    {
        var screen = new PixelRect(0, 0, 800, 600);
        var selection = new PixelRect(250, 200, 300, 200);

        var result = ResultPanelPlacement.Place(selection, 400, 500, screen, 12);

        Assert.Equal(new PixelRect(250, 100, 400, 500), result);
    }

    [Fact]
    public void Panel_stays_above_a_bottom_taskbar_work_area()
    {
        var workArea = new PixelRect(0, 0, 1920, 1040);
        var selection = new PixelRect(120, 930, 480, 110);

        var result = ResultPanelPlacement.Place(selection, 520, 260, workArea, 12);

        Assert.Equal(selection.Top - 12, result.Bottom);
        Assert.True(result.Top >= workArea.Top);
        Assert.True(result.Bottom <= workArea.Bottom);
    }

    [Fact]
    public void Panel_stays_right_of_a_side_taskbar_work_area()
    {
        var workArea = new PixelRect(80, 0, 1840, 1080);
        var selection = new PixelRect(10, 100, 300, 150);

        var result = ResultPanelPlacement.Place(selection, 520, 260, workArea, 12);

        Assert.Equal(workArea.Left, result.Left);
        Assert.True(result.Right <= workArea.Right);
        Assert.True(result.Bottom <= workArea.Bottom);
    }
}
