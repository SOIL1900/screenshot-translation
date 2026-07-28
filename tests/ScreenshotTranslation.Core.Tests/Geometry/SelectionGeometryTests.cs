using ScreenshotTranslation.Core.Geometry;

namespace ScreenshotTranslation.Core.Tests.Geometry;

public sealed class SelectionGeometryTests
{
    [Fact]
    public void Pixel_rect_uses_half_open_bounds()
    {
        var rect = new PixelRect(10, 20, 30, 40);

        Assert.Equal(10, rect.Left);
        Assert.Equal(20, rect.Top);
        Assert.Equal(40, rect.Right);
        Assert.Equal(60, rect.Bottom);
        Assert.True(rect.Contains(new PixelPoint(10, 20)));
        Assert.True(rect.Contains(new PixelPoint(39, 59)));
        Assert.False(rect.Contains(new PixelPoint(40, 59)));
        Assert.False(rect.Contains(new PixelPoint(39, 60)));
    }

    [Theory]
    [InlineData(100, 100, 250, 220, 100, 100, 150, 120)]
    [InlineData(250, 220, 100, 100, 100, 100, 150, 120)]
    [InlineData(250, 100, 100, 220, 100, 100, 150, 120)]
    [InlineData(100, 220, 250, 100, 100, 100, 150, 120)]
    public void Create_normalizes_drag_direction(
        int startX,
        int startY,
        int endX,
        int endY,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        var bounds = new PixelRect(0, 0, 1000, 800);

        var actual = SelectionGeometry.Create(
            new PixelPoint(startX, startY),
            new PixelPoint(endX, endY),
            bounds);

        Assert.Equal(new PixelRect(expectedX, expectedY, expectedWidth, expectedHeight), actual);
    }

    [Fact]
    public void Create_clips_both_drag_points_to_screen_bounds()
    {
        var bounds = new PixelRect(50, 40, 300, 200);

        var actual = SelectionGeometry.Create(
            new PixelPoint(400, 300),
            new PixelPoint(0, 0),
            bounds);

        Assert.Equal(bounds, actual);
    }

    [Theory]
    [InlineData(25, 30, 125, 130)]
    [InlineData(-500, 0, 0, 100)]
    [InlineData(0, -500, 100, 0)]
    [InlineData(1000, 0, 900, 100)]
    [InlineData(0, 1000, 100, 700)]
    public void Move_preserves_size_and_clamps_to_bounds(int dx, int dy, int expectedX, int expectedY)
    {
        var bounds = new PixelRect(0, 0, 1000, 800);

        var actual = SelectionGeometry.Move(new PixelRect(100, 100, 100, 100), dx, dy, bounds);

        Assert.Equal(new PixelRect(expectedX, expectedY, 100, 100), actual);
    }

    [Theory]
    [InlineData(ResizeHandle.TopLeft, -10, -20, 90, 80, 110, 120)]
    [InlineData(ResizeHandle.Top, 0, -20, 100, 80, 100, 120)]
    [InlineData(ResizeHandle.TopRight, 10, -20, 100, 80, 110, 120)]
    [InlineData(ResizeHandle.Right, 10, 0, 100, 100, 110, 100)]
    [InlineData(ResizeHandle.BottomRight, 10, 20, 100, 100, 110, 120)]
    [InlineData(ResizeHandle.Bottom, 0, 20, 100, 100, 100, 120)]
    [InlineData(ResizeHandle.BottomLeft, -10, 20, 90, 100, 110, 120)]
    [InlineData(ResizeHandle.Left, -10, 0, 90, 100, 110, 100)]
    public void Resize_updates_the_requested_edges(
        ResizeHandle handle,
        int dx,
        int dy,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        var bounds = new PixelRect(0, 0, 1000, 800);

        var actual = SelectionGeometry.Resize(
            new PixelRect(100, 100, 100, 100),
            handle,
            dx,
            dy,
            bounds,
            24);

        Assert.Equal(new PixelRect(expectedX, expectedY, expectedWidth, expectedHeight), actual);
    }

    [Theory]
    [InlineData(ResizeHandle.TopLeft, 200, 200, 176, 176, 24, 24)]
    [InlineData(ResizeHandle.Top, 0, 200, 100, 176, 100, 24)]
    [InlineData(ResizeHandle.TopRight, -200, 200, 100, 176, 24, 24)]
    [InlineData(ResizeHandle.Right, -200, 0, 100, 100, 24, 100)]
    [InlineData(ResizeHandle.BottomRight, -200, -200, 100, 100, 24, 24)]
    [InlineData(ResizeHandle.Bottom, 0, -200, 100, 100, 100, 24)]
    [InlineData(ResizeHandle.BottomLeft, 200, -200, 176, 100, 24, 24)]
    [InlineData(ResizeHandle.Left, 200, 0, 176, 100, 24, 100)]
    public void Resize_clamps_all_handles_to_the_minimum_size(
        ResizeHandle handle,
        int dx,
        int dy,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        var bounds = new PixelRect(0, 0, 1000, 800);

        var actual = SelectionGeometry.Resize(
            new PixelRect(100, 100, 100, 100),
            handle,
            dx,
            dy,
            bounds,
            24);

        Assert.Equal(new PixelRect(expectedX, expectedY, expectedWidth, expectedHeight), actual);
    }

    [Theory]
    [InlineData(ResizeHandle.TopLeft, -500, -500, 0, 0, 200, 200)]
    [InlineData(ResizeHandle.TopRight, 5000, -500, 100, 0, 900, 200)]
    [InlineData(ResizeHandle.BottomRight, 5000, 5000, 100, 100, 900, 700)]
    [InlineData(ResizeHandle.BottomLeft, -500, 5000, 0, 100, 200, 700)]
    public void Resize_clamps_requested_edges_to_bounds(
        ResizeHandle handle,
        int dx,
        int dy,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        var bounds = new PixelRect(0, 0, 1000, 800);

        var actual = SelectionGeometry.Resize(
            new PixelRect(100, 100, 100, 100),
            handle,
            dx,
            dy,
            bounds,
            24);

        Assert.Equal(new PixelRect(expectedX, expectedY, expectedWidth, expectedHeight), actual);
    }

    [Fact]
    public void Resize_with_no_handle_leaves_the_selection_unchanged()
    {
        var selection = new PixelRect(100, 100, 100, 100);

        var actual = SelectionGeometry.Resize(
            selection,
            ResizeHandle.None,
            500,
            500,
            new PixelRect(0, 0, 1000, 800),
            24);

        Assert.Equal(selection, actual);
    }
}
