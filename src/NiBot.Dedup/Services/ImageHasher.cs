using System.Collections.Concurrent;
using Aiursoft.Canon;
using CoenM.ImageHash.HashAlgorithms;
using Microsoft.Extensions.Logging;
using NiBot.Dedup.Models;

namespace NiBot.Dedup.Services;

public class ImageHasher(ILogger<DedupEngine> logger, CanonPool canonPool)
{
    public async Task<MappedImage[]> MapImagesAsync(IEnumerable<string> imagePaths)
    {
        var hashAlgo = new PerceptualHash();
        ConcurrentBag<MappedImage> mappedImages = new();
        foreach (var image in imagePaths)
        {
            canonPool.RegisterNewTaskToPool(async () =>
            {
                try
                {
                    var mappedImage = await MappedImage.CreateAsync(image, hashAlgo);
                    mappedImages.Add(mappedImage);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to map image {image}.", image);
                }
            });
        }

        await canonPool.RunAllTasksInPoolAsync(Environment.ProcessorCount);
        return mappedImages.ToArray();
    }
}

public static class ArrayExtensions
{
    public static IEnumerable<(T left, T right)> YieldPairs<T>(this T[] array)
    {
        for (var i = 0; i < array.Length; i++)
        {
            for (var j = i + 1; j < array.Length; j++)
            {
                yield return (array[i], array[j]);
            }
        }
    }
}