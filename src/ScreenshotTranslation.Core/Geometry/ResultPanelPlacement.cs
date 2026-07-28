namespace ScreenshotTranslation.Core.Geometry;

public static class ResultPanelPlacement
{
    public static PixelRect Place(
        PixelRect selection,
        int panelWidth,
        int panelHeight,
        PixelRect screen,
        int gap)
    {
        var maximumX = screen.Right - panelWidth;
        var maximumY = screen.Bottom - panelHeight;
        var x = Clamp(selection.Left, screen.Left, maximumX);

        var belowY = (long)selection.Bottom + gap;
        var aboveY = (long)selection.Top - gap - panelHeight;

        int y;
        if (belowY >= screen.Top && belowY + panelHeight <= screen.Bottom)
        {
            y = (int)belowY;
        }
        else if (aboveY >= screen.Top && aboveY + panelHeight <= screen.Bottom)
        {
            y = (int)aboveY;
        }
        else
        {
            y = Clamp(belowY, screen.Top, maximumY);
        }

        return new PixelRect(x, y, panelWidth, panelHeight);
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
