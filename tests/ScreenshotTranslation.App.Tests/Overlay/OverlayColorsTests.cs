using System.Windows;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfColor = System.Windows.Media.Color;

namespace ScreenshotTranslation.App.Tests.Overlay;

public sealed class OverlayColorsTests
{
    [Fact]
    public void Overlay_dictionary_defines_accessible_selected_item_colors_locally()
    {
        StaTestHost.Run(() =>
        {
            var resources = Assert.IsType<ResourceDictionary>(WpfApplication.LoadComponent(
                new Uri(
                    "/ScreenshotTranslation;component/Overlay/OverlayColors.xaml",
                    UriKind.Relative)));

            Assert.True(
                resources.Contains("Brush.PrimaryTint"),
                "OverlayColors must define Brush.PrimaryTint in its own resource dictionary.");
            Assert.True(
                resources.Contains("Overlay.Brush.TextPrimary"),
                "OverlayColors must define its intended selected-item text brush locally.");

            var selectedBackground = Assert.IsType<SolidColorBrush>(resources["Brush.PrimaryTint"]);
            var selectedText = Assert.IsType<SolidColorBrush>(resources["Overlay.Brush.TextPrimary"]);

            Assert.Equal(WpfColor.FromRgb(0x2E, 0x10, 0x65), selectedBackground.Color);
            Assert.Equal(WpfColor.FromRgb(0xF1, 0xF5, 0xF9), selectedText.Color);
            var contrastRatio = ContrastRatio(selectedText.Color, selectedBackground.Color);
            Assert.True(
                contrastRatio >= 7d,
                $"Overlay selected ComboBox items must retain AAA text contrast in either app theme; actual ratio was {contrastRatio:F2}:1.");
        });
    }

    private static double ContrastRatio(WpfColor first, WpfColor second)
    {
        var lighter = Math.Max(RelativeLuminance(first), RelativeLuminance(second));
        var darker = Math.Min(RelativeLuminance(first), RelativeLuminance(second));
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static double RelativeLuminance(WpfColor color)
    {
        return (0.2126d * Linearize(color.R)) +
               (0.7152d * Linearize(color.G)) +
               (0.0722d * Linearize(color.B));
    }

    private static double Linearize(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045d
            ? value / 12.92d
            : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
    }

    private static class StaTestHost
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public static void Run(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            })
            {
                IsBackground = true,
                Name = "ScreenshotTranslation.OverlayColorsTests.STA",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            Assert.True(thread.Join(Timeout), "The STA overlay color test thread did not stop.");
            if (failure is not null)
            {
                throw new Xunit.Sdk.XunitException(
                    $"The STA overlay color check failed: {failure.GetType().Name}: {failure.Message}");
            }
        }
    }
}
