using System.Collections.Concurrent;
using Aiursoft.Canon;
using Aiursoft.NiBot.Core;
using CoenM.ImageHash.HashAlgorithms;
using Microsoft.Extensions.Logging;
using NiBot.Dedup.Models;

namespace NiBot.Dedup.Services;

public class ImageHasher(ILogger<DedupEngine> logger, CanonPool canonPool)
{
    public async Task<MappedImage[]> MapImagesAsync(string[] imagePaths, bool showProgress)
    {
        var hashAlgo = new PerceptualHash();
        ConcurrentBag<MappedImage> mappedImages = new();
        
        // Progress bar.
        ProgressBar? bar = null;
        if (showProgress) bar = new ProgressBar();
        var totalTasks = imagePaths.Length;
        var completedTasks = 0;
        
        foreach (var image in imagePaths)
        {
            canonPool.RegisterNewTaskToPool(async () =>
            {
                try
                {
                    var mappedImage = await MappedImage.CreateAsync(image, hashAlgo);
                    mappedImages.Add(mappedImage);
                    if (showProgress)
                    {
                        Interlocked.Increment(ref completedTasks);
                        bar?.Report((double)completedTasks / totalTasks);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to map image {image}.", image);
                }
            });
        }
        
        await canonPool.RunAllTasksInPoolAsync(Environment.ProcessorCount);
        bar?.Dispose();
        return mappedImages.ToArray();
    }
}