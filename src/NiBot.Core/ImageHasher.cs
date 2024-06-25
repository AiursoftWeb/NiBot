using System.Collections.Concurrent;
using Aiursoft.Canon;
using CoenM.ImageHash.HashAlgorithms;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NiBot.Core;

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
                var mappedImage = await MappedImage.CreateAsync(image, hashAlgo);
                mappedImages.Add(mappedImage);
            });
        }

        await canonPool.RunAllTasksInPoolAsync(Environment.ProcessorCount);
        return mappedImages.ToArray();
    }
}