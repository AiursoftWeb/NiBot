using Microsoft.ML.OnnxRuntime;
using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Aiursoft.NiBot.Archive;

/// <summary>CPU CLIP vision encoder. All references and imports use the same preprocessing.</summary>
public sealed class OnnxClipEmbeddingProvider : IImageEmbeddingProvider
{
    private readonly InferenceSession _session;
    public string Fingerprint { get; }
    public const int ImageSize = 224;

    public OnnxClipEmbeddingProvider(string modelPath, int threads = 2)
    {
        if (threads < 1) throw new ArgumentOutOfRangeException(nameof(threads));
        ArchiveFiles.AssertNoLinks(modelPath);
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("CLIP ONNX model not found. Install a compatible ONNX model with archive-model or specify --model; see docs/archive.md.", modelPath);
        // Self-contained ONNX only: all model weights participate in cache invalidation.
        // Load from bytes so external tensor files cannot be resolved using a model file path.
        var modelBytes = File.ReadAllBytes(modelPath);
        Fingerprint = $"clip-rgb-bicubic-center224-v1-imagesharp-{typeof(Image).Assembly.GetName().Version}:" + Convert.ToHexStringLower(SHA256.HashData(modelBytes));
        using var options = new SessionOptions();
        options.IntraOpNumThreads = threads;
        options.InterOpNumThreads = 1;
        _session = new InferenceSession(modelBytes, options);
        if (!_session.ModelMetadata.CustomMetadataMap.TryGetValue("nibot.archive", out var contract) || contract != "clip-vision-v1" ||
            !_session.InputMetadata.TryGetValue("pixel_values", out var input) || input.ElementType != typeof(float) ||
            input.Dimensions.Length != 4 || input.Dimensions[1] != 3 || input.Dimensions[2] != ImageSize || input.Dimensions[3] != ImageSize ||
            !_session.OutputMetadata.TryGetValue("image_features", out var output) || output.ElementType != typeof(float) || output.Dimensions.Length != 2)
        {
            _session.Dispose();
            throw new InvalidDataException("Unsupported model contract. See the documented CLIP vision model contract.");
        }
    }

    public float[] Embed(string path)
    {
        var pixels = Preprocess(path);
        using var input = OrtValue.CreateTensorValueFromMemory(pixels, [1, 3, ImageSize, ImageSize]);
        using var runOptions = new RunOptions();
        using var results = _session.Run(runOptions, new Dictionary<string, OrtValue> { ["pixel_values"] = input }, ["image_features"]);
        var vector = results[0].GetTensorDataAsSpan<float>().ToArray();
        return CategoryMatcher.Normalize(vector);
    }

    public static float[] Preprocess(string path)
    {
        using var image = Image.Load<Rgb24>(path);
        // Match the original CLIP pipeline: RGB, shortest side -> 224, center crop,
        // channel-first float RGB and the model's mean/std. No EXIF auto-rotation.
        var scale = ImageSize / (double)Math.Min(image.Width, image.Height);
        var width = Math.Max(ImageSize, (int)(image.Width * scale));
        var height = Math.Max(ImageSize, (int)(image.Height * scale));
        image.Mutate(context => context.Resize(width, height, KnownResamplers.Bicubic)
            .Crop(new Rectangle((width - ImageSize) / 2, (height - ImageSize) / 2, ImageSize, ImageSize)));
        float[] mean = [0.48145466f, 0.4578275f, 0.40821073f];
        float[] std = [0.26862954f, 0.26130258f, 0.27577711f];
        var plane = ImageSize * ImageSize;
        var pixels = new float[3 * plane];
        for (var y = 0; y < ImageSize; y++)
        for (var x = 0; x < ImageSize; x++)
        {
            var pixel = image[x, y];
            var i = y * ImageSize + x;
            pixels[i] = (pixel.R / 255f - mean[0]) / std[0];
            pixels[plane + i] = (pixel.G / 255f - mean[1]) / std[1];
            pixels[2 * plane + i] = (pixel.B / 255f - mean[2]) / std[2];
        }
        return pixels;
    }

    public void Dispose() => _session.Dispose();
}
