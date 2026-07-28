using ScreenshotTranslation.Core.Geometry;

namespace ScreenshotTranslation.App.Overlay;

internal sealed record OverlayPanelLayoutResult(
    double WidthDip,
    double HeightDip,
    PixelRect PhysicalBounds);

internal static class OverlayPanelLayout
{
    public static OverlayPanelLayoutResult Calculate(
        PixelRect selection,
        PixelRect frameLocalWorkArea,
        OverlayCoordinateMapper coordinateMapper,
        double desiredHeightDip)
    {
        ArgumentNullException.ThrowIfNull(coordinateMapper);
        if (frameLocalWorkArea.Width <= 0 || frameLocalWorkArea.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameLocalWorkArea),
                "The monitor work area must have positive dimensions.");
        }
        if (!double.IsFinite(desiredHeightDip) || desiredHeightDip <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(desiredHeightDip),
                "The desired panel height must be a positive finite number.");
        }

        var workAreaDip = coordinateMapper.ToDip(frameLocalWorkArea);
        var selectionDip = coordinateMapper.ToDip(selection);
        var widthDip = Math.Min(
            workAreaDip.Width,
            Math.Clamp(
                selectionDip.Width,
                OverlayViewModel.PanelMinimumWidth,
                OverlayViewModel.PanelMaximumWidth));
        var heightDip = Math.Min(
            workAreaDip.Height,
            Math.Clamp(
                desiredHeightDip,
                OverlayViewModel.PanelMinimumHeight,
                OverlayViewModel.PanelMaximumHeight));
        var widthPhysical = Math.Min(
            frameLocalWorkArea.Width,
            coordinateMapper.DipLengthToPhysicalX(widthDip));
        var heightPhysical = Math.Min(
            frameLocalWorkArea.Height,
            coordinateMapper.DipLengthToPhysicalY(heightDip));
        var physicalBounds = ResultPanelPlacement.Place(
            selection,
            widthPhysical,
            heightPhysical,
            frameLocalWorkArea,
            OverlayViewModel.PanelGap);

        return new OverlayPanelLayoutResult(widthDip, heightDip, physicalBounds);
    }
}
