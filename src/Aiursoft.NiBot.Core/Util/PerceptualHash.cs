/*
Adapted from https://github.com/coenm/ImageHash (v1.3.6).
Image decoding and resampling use SkiaSharp.

MIT License

Copyright (c) 2017 Coen Munckhof

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
namespace Aiursoft.NiBot.Core.Util
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using SkiaSharp;

    /// <summary>
    /// Perceptual hash; Calculate a hash of an image by first transforming the image to an 64x64 grayscale bitmap and then using the Discrete cosine transform to remove the high frequencies.
    /// </summary>
    public sealed class PerceptualHash
    {
        private const int SIZE = 64;
        private static readonly double _sqrt2DivSize = Math.Sqrt(2D / SIZE);
        private static readonly double _sqrt2 = 1 / Math.Sqrt(2);
        private static readonly List<Vector<double>>[] _dctCoeffsSimd = GenerateDctCoeffsSimd();

        public ulong Hash(SKBitmap image, SKEncodedOrigin origin = SKEncodedOrigin.TopLeft)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            var rows = new double[SIZE, SIZE];
            var sequence = new double[SIZE];
            var matrix = new double[SIZE, SIZE];

            // Mitchell smooths the thumbnail before the low-frequency DCT comparison.
            using var resized = image.Resize(new SKImageInfo(SIZE, SIZE, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
                ?? throw new InvalidDataException("Failed to resize image for hashing.");

            // Calculate the DCT for each row.
            for (var y = 0; y < SIZE; y++)
            {
                for (var x = 0; x < SIZE; x++)
                {
                    // The previous algorithm applies EXIF orientation after resizing.
                    var (sourceX, sourceY) = origin switch
                    {
                        SKEncodedOrigin.TopRight => (SIZE - 1 - x, y),
                        SKEncodedOrigin.BottomRight => (SIZE - 1 - x, SIZE - 1 - y),
                        SKEncodedOrigin.BottomLeft => (x, SIZE - 1 - y),
                        SKEncodedOrigin.LeftTop => (y, x),
                        SKEncodedOrigin.RightTop => (y, SIZE - 1 - x),
                        SKEncodedOrigin.RightBottom => (SIZE - 1 - y, SIZE - 1 - x),
                        SKEncodedOrigin.LeftBottom => (SIZE - 1 - y, x),
                        _ => (x, y)
                    };
                    var pixel = resized.GetPixel(sourceX, sourceY);
                    sequence[x] = Math.Round(0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue);
                }

                Dct1D_SIMD(sequence, rows, y);
            }

            // Calculate the DCT for each column.
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < SIZE; y++)
                {
                    sequence[y] = rows[y, x];
                }

                Dct1D_SIMD(sequence, matrix, x, limit: 8);
            }

            // Only use the top 8x8 values.
            var top8X8 = new double[SIZE];
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    top8X8[(y * 8) + x] = matrix[y, x];
                }
            }

            // Get Median.
            var median = CalculateMedian64Values(top8X8);

            // Calculate hash.
            var mask = 1UL << (SIZE - 1);
            var hash = 0UL;

            for (var i = 0; i < SIZE; i++)
            {
                if (top8X8[i] > median)
                {
                    hash |= mask;
                }

                mask >>= 1;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double CalculateMedian64Values(IReadOnlyCollection<double> values)
        {
            Debug.Assert(values.Count == 64, "This DCT method works with 64 doubles.");
            return values.OrderBy(value => value).Skip(31).Take(2).Average();
        }

        private static List<Vector<double>>[] GenerateDctCoeffsSimd()
        {
            var results = new List<Vector<double>>[SIZE];
            for (var coef = 0; coef < SIZE; coef++)
            {
                var singleResultRaw = new double[SIZE];
                for (var i = 0; i < SIZE; i++)
                {
                    singleResultRaw[i] = Math.Cos(((2.0 * i) + 1.0) * coef * Math.PI / (2.0 * SIZE));
                }

                var singleResultList = new List<Vector<double>>();
                var stride = Vector<double>.Count;
                Debug.Assert(SIZE % stride == 0, "Size must be a multiple of SIMD stride");
                for (var i = 0; i < SIZE; i += stride)
                {
                    var v = new Vector<double>(singleResultRaw, i);
                    singleResultList.Add(v);
                }

                results[coef] = singleResultList;
            }

            return results;
        }

        /// <summary>
        /// One dimensional Discrete Cosine Transformation.
        /// </summary>
        /// <param name="valuesRaw">Should be an array of doubles of length 64.</param>
        /// <param name="coefficients">Coefficients.</param>
        /// <param name="ci">Coefficients index.</param>
        /// <param name="limit">Limit.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Dct1D_SIMD(double[] valuesRaw, double[,] coefficients, int ci, int limit = SIZE)
        {
            Debug.Assert(valuesRaw.Length == 64, "This DCT method works with 64 doubles.");

            var valuesList = new List<Vector<double>>();
            var stride = Vector<double>.Count;
            for (var i = 0; i < valuesRaw.Length; i += stride)
            {
                valuesList.Add(new Vector<double>(valuesRaw, i));
            }

            for (var coef = 0; coef < limit; coef++)
            {
                for (var i = 0; i < valuesList.Count; i++)
                {
                    coefficients[ci, coef] += Vector.Dot(valuesList[i], _dctCoeffsSimd[coef][i]);
                }

                coefficients[ci, coef] *= _sqrt2DivSize;
                if (coef == 0)
                {
                    coefficients[ci, coef] *= _sqrt2;
                }
            }
        }
    }
}
