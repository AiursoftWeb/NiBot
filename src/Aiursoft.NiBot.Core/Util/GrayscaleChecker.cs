﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Aiursoft.NiBot.Core.Util;

public static class GrayscaleChecker
{
    public static bool IsImageGrayscale(Image<Rgba32> image, long totalPixelCount)
    {
        var differentPixelCount = 0;
        for (var y = 0; y < image.Height; y += 2) // half resampling
        {
            for (var x = 0; x < image.Width; x += 2)
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
        return differentPixelCount < (totalPixelCount >> 10); // If less than 0x100 pixels are different, it's grayscale.
    }
    
    private static bool DifferentColorNumber(byte rgbValueLeft, byte rgbValueRight)
    {
        // If a pixel has a difference of more than 0x20 between two of its RGB values, it's not grayscale.
        return Math.Abs(rgbValueLeft - rgbValueRight) >> 5 > 0;
    }
}