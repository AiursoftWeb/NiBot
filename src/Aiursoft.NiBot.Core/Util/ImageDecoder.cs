using SkiaSharp;

namespace Aiursoft.NiBot.Core.Util;

public static class ImageDecoder
{
    public static SKBitmap Load(string path) => Load(path, out _);

    // RGB consumers ignore transparency before resampling, as with the former Rgb24 decoder.
    public static SKBitmap LoadRgb(string path)
    {
        var bitmap = Load(path);
        // Decoding is explicitly RGBA/unpremultiplied. Drop alpha without compositing
        // or premultiplying the RGB channels during a native color-type conversion.
        var pixels = bitmap.GetPixelSpan();
        for (var index = 3; index < pixels.Length; index += 4) pixels[index] = 255;
        return bitmap;
    }

    public static SKBitmap Load(string path, out SKEncodedOrigin origin)
    {
        using var codec = SKCodec.Create(path);
        return Decode(codec, out origin);
    }

    public static SKBitmap Load(byte[] bytes, out SKEncodedOrigin origin)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        return Decode(codec, out origin);
    }

    private static SKBitmap Decode(SKCodec? codec, out SKEncodedOrigin origin)
    {
        if (codec == null) throw new InvalidDataException("Unsupported or invalid image.");
        origin = codec.EncodedOrigin;
        var bitmap = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height,
            SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success) return bitmap;
        bitmap.Dispose();
        throw new InvalidDataException("Failed to decode image pixels.");
    }

    public static SKBitmap Resize(SKBitmap source, int width, int height, SKRect? bounds = null)
    {
        var result = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(result);
        using var image = SKImage.FromBitmap(source);
        using var paint = new SKPaint();
        paint.BlendMode = SKBlendMode.Src;
        canvas.DrawImage(image, bounds ?? new SKRect(0, 0, source.Width, source.Height),
            new SKRect(0, 0, width, height), new SKSamplingOptions(SKCubicResampler.CatmullRom), paint);
        return result;
    }
}
