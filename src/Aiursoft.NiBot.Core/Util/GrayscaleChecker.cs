using SkiaSharp;

namespace Aiursoft.NiBot.Core.Util;

public static class GrayscaleChecker
{
    public static bool IsImageGrayscale(SKBitmap image, long totalPixelCount)
    {
        var differentPixelCount = 0;
        for (var y = 0; y < image.Height; y += 2) // half resampling
        {
            for (var x = 0; x < image.Width; x += 2)
            {
                var pixel = image.GetPixel(x, y);
                if (
                    DifferentColorNumber(pixel.Red, pixel.Green) ||
                    DifferentColorNumber(pixel.Green, pixel.Blue) ||
                    DifferentColorNumber(pixel.Red, pixel.Blue))
                {
                    differentPixelCount++;
                }
            }
        }
        return differentPixelCount < (totalPixelCount >> 10); // If less than 0x100 pixels are different, it's grayscale.
    }

    private static bool DifferentColorNumber(byte rgbValueLeft, byte rgbValueRight)
    {
        // If a pixel has a difference of more than 0x20 between two of its RGB values, it's not grayscale.
        return Math.Abs(rgbValueLeft - rgbValueRight) >> 5 > 0;
    }
}
