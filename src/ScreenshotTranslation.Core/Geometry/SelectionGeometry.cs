namespace ScreenshotTranslation.Core.Geometry;

public static class SelectionGeometry
{
    public static PixelRect Create(PixelPoint start, PixelPoint end, PixelRect bounds)
    {
        var startX = Clamp(start.X, bounds.Left, bounds.Right);
        var startY = Clamp(start.Y, bounds.Top, bounds.Bottom);
        var endX = Clamp(end.X, bounds.Left, bounds.Right);
        var endY = Clamp(end.Y, bounds.Top, bounds.Bottom);

        var left = Math.Min(startX, endX);
        var top = Math.Min(startY, endY);
        var right = Math.Max(startX, endX);
        var bottom = Math.Max(startY, endY);

        return new PixelRect(left, top, right - left, bottom - top);
    }

    public static PixelRect Move(PixelRect selection, int dx, int dy, PixelRect bounds)
    {
        var maximumX = bounds.Right - selection.Width;
        var maximumY = bounds.Bottom - selection.Height;
        var x = Clamp((long)selection.X + dx, bounds.Left, maximumX);
        var y = Clamp((long)selection.Y + dy, bounds.Top, maximumY);

        return new PixelRect(x, y, selection.Width, selection.Height);
    }

    public static PixelRect Resize(
        PixelRect selection,
        ResizeHandle handle,
        int dx,
        int dy,
        PixelRect bounds,
        int minimumSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumSize);

        var left = selection.Left;
        var top = selection.Top;
        var right = selection.Right;
        var bottom = selection.Bottom;

        switch (handle)
        {
            case ResizeHandle.None:
                break;
            case ResizeHandle.TopLeft:
                left = Clamp((long)left + dx, bounds.Left, right - minimumSize);
                top = Clamp((long)top + dy, bounds.Top, bottom - minimumSize);
                break;
            case ResizeHandle.Top:
                top = Clamp((long)top + dy, bounds.Top, bottom - minimumSize);
                break;
            case ResizeHandle.TopRight:
                top = Clamp((long)top + dy, bounds.Top, bottom - minimumSize);
                right = Clamp((long)right + dx, left + minimumSize, bounds.Right);
                break;
            case ResizeHandle.Right:
                right = Clamp((long)right + dx, left + minimumSize, bounds.Right);
                break;
            case ResizeHandle.BottomRight:
                right = Clamp((long)right + dx, left + minimumSize, bounds.Right);
                bottom = Clamp((long)bottom + dy, top + minimumSize, bounds.Bottom);
                break;
            case ResizeHandle.Bottom:
                bottom = Clamp((long)bottom + dy, top + minimumSize, bounds.Bottom);
                break;
            case ResizeHandle.BottomLeft:
                left = Clamp((long)left + dx, bounds.Left, right - minimumSize);
                bottom = Clamp((long)bottom + dy, top + minimumSize, bounds.Bottom);
                break;
            case ResizeHandle.Left:
                left = Clamp((long)left + dx, bounds.Left, right - minimumSize);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(handle), handle, "Unknown resize handle.");
        }

        return new PixelRect(left, top, right - left, bottom - top);
    }

    private static int Clamp(long value, int minimum, int maximum)
    {
        if (maximum < minimum)
        {
            return minimum;
        }

        return (int)Math.Clamp(value, minimum, maximum);
    }
}
