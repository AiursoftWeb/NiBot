using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NiBot.Dedup.Util;

public static class GrayscaleChecker
{
    public static bool IsImageGrayscale(Image<Rgba32> image, long totalPixelCount)
    {
        var differentPixelCount = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                if (
                    DifferentColorNumber(pixel.R, pixel.G) || 
                    DifferentColorNumber(pixel.G, pixel.B) ||
                    DifferentColorNumber(pixel.R, pixel.B))
                {
                    differentPixelCount++;
                }
            }
        }
        //return 100 * differentPixelCount < 2 * totalPixelCount;
        //return differentPixelCount / (double)totalPixelCount < 0.03;
        return differentPixelCount < (totalPixelCount >> 6); // If less than 1/64 pixels are different, it's grayscale.
    }
    
    private static bool DifferentColorNumber(byte rgbValueLeft, byte rgbValueRight)
    {
        // If a pixel has a difference of more than 30 between two of its RGB values, it's not grayscale.
        return Math.Abs(rgbValueLeft - rgbValueRight) > 30;
    }
}