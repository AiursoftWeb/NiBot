using Aiursoft.NiBot.Core.Util;
using SkiaSharp;

namespace Aiursoft.NiBot.Archive;

/// <summary>Conservative review hints, never proof of identical content.</summary>
public sealed class VisualDuplicateDetector
{
    private readonly Dictionary<string, float[][]> _signatures = new(StringComparer.Ordinal);

    public bool IsPossibleDuplicate(string incoming, string reference)
    {
        var left = Get(incoming);
        var right = Get(reference);
        return left.Any(a => right.Any(b => Correlation(a, b) >= 0.995));
    }

    private float[][] Get(string path)
    {
        if (_signatures.TryGetValue(path, out var cached)) return cached;
        using var original = ImageDecoder.LoadRgb(path);
        var size = Math.Min(original.Width, original.Height);
        var left = (original.Width - size) / 2;
        var top = (original.Height - size) / 2;
        var result = new[] { Signature(original), Signature(original, new SKRect(left, top, left + size, top + size)) };
        _signatures.Add(path, result);
        return result;
    }

    private static float[] Signature(SKBitmap source, SKRect? bounds = null)
    {
        using var image = ImageDecoder.Resize(source, 64, 64, bounds);
        var result = new float[64 * 64];
        for (var y = 0; y < 64; y++)
        for (var x = 0; x < 64; x++)
        {
            var p = image.GetPixel(x, y);
            result[y * 64 + x] = (0.299f * p.Red + 0.587f * p.Green + 0.114f * p.Blue) / 255;
        }
        return result;
    }

    private static double Correlation(float[] left, float[] right)
    {
        var lm = left.Average();
        var rm = right.Average();
        double sum = 0, lv = 0, rv = 0;
        for (var i = 0; i < left.Length; i++)
        {
            var a = left[i] - lm;
            var b = right[i] - rm;
            sum += a * b;
            lv += a * a;
            rv += b * b;
        }
        // Flat or nearly flat images provide too little evidence for a duplicate hint.
        if (lv / left.Length < 0.0025 || rv / right.Length < 0.0025) return 0;
        return sum / Math.Sqrt(lv * rv);
    }
}
