using System.Collections.Concurrent;
using Aiursoft.Canon;
using Aiursoft.CommandFramework.Models;
using Aiursoft.NiBot.Dedup.Models;
using CoenM.ImageHash.HashAlgorithms;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NiBot.Dedup.Services;

public class ImageHasher(ILogger<DedupEngine> logger, CanonPool canonPool)
{
    public async Task<MappedImage[]> MapImagesAsync(string[] imagePaths, bool showProgress, int threads)
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
                        // ReSharper disable once AccessToDisposedClosure
                        bar?.Report((double)completedTasks / totalTasks);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to map image {image}.", image);
                }
            });
        }
        
        await canonPool.RunAllTasksInPoolAsync(threads);
        bar?.Dispose();
        
        var mappedImagesArray = mappedImages.ToArray();
        // Set the ID as the index. This is essential for building DSU.
        for (var i = 0; i < mappedImagesArray.Length; i++)
        {
            mappedImagesArray[i].Id = i;
        }
        return mappedImagesArray;
    }
}