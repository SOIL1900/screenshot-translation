using System.Drawing.Imaging;
using ScreenshotTranslation.Infrastructure.Translation;

namespace ScreenshotTranslation.Infrastructure.Tests.Translation;

public sealed class PngRequestImageNormalizerTests
{
    [Fact]
    public void Small_png_is_returned_unchanged()
    {
        var input = TestPngFactory.CreateSolid(640, 360);

        var output = PngRequestImageNormalizer.Normalize(input);

        Assert.Equal(input, output);
    }

    [Fact]
    public void Four_k_png_is_resized_proportionally_to_the_long_edge_limit()
    {
        var input = TestPngFactory.CreateSolid(3840, 2160);

        var output = PngRequestImageNormalizer.Normalize(input);
        var image = TestPngFactory.Inspect(output);

        Assert.Equal(PngRequestImageNormalizer.MaxLongEdgePixels, image.Width);
        Assert.Equal(1152, image.Height);
        Assert.True(output.Length <= PngRequestImageNormalizer.MaxEncodedPngBytes);
    }

    [Fact]
    public void High_entropy_png_is_iteratively_reduced_below_the_payload_limit()
    {
        var input = TestPngFactory.CreateHighEntropy(2048, 2048);
        Assert.True(input.Length > PngRequestImageNormalizer.MaxEncodedPngBytes);

        var output = PngRequestImageNormalizer.Normalize(input);
        var image = TestPngFactory.Inspect(output);

        Assert.True(output.Length <= PngRequestImageNormalizer.MaxEncodedPngBytes);
        Assert.True(image.Width < 2048);
        Assert.Equal(image.Width, image.Height);
    }

    [Fact]
    public void Resized_output_is_a_valid_png_and_is_never_upscaled()
    {
        var input = TestPngFactory.CreateHighEntropy(1200, 800);

        var output = PngRequestImageNormalizer.Normalize(input);
        var image = TestPngFactory.Inspect(output);

        Assert.Equal(ImageFormat.Png.Guid, image.Format);
        Assert.True(image.Width <= 1200);
        Assert.True(image.Height <= 800);
    }
}
