namespace ScreenshotTranslation.Core.Geometry;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Left => X;

    public int Top => Y;

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(PixelPoint point) =>
        point.X >= Left &&
        point.X < Right &&
        point.Y >= Top &&
        point.Y < Bottom;
}
