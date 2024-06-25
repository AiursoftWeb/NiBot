using System.Numerics;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NiBot.Core;

public class DedupEngine(ILogger<DedupEngine> logger, ImageHasher imageHasher)
{
    public async Task DedupAsync(string path, int similarityBar, bool recursive, KeepPreference keep, DuplicateAction action, bool interactive, string[] extensions)
    {
        logger.LogInformation("Start de-duplicating images in {path}. Minimum similarity bar is {similarityBar}. Recursive: {recursive}. Keep: {keep}. Action: {action}. Interactive: {interactive}. Extensions: {extensions}.",
            path, similarityBar, recursive, keep, action, interactive, string.Join(", ", extensions));
        
        var files = Directory.GetFiles(path, "*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        var images = files
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        logger.LogTrace("Found {Count} images in {path}. Calculating hashes...", images.Length, path);
        var mappedImages = await imageHasher.MapImagesAsync(images);
        
        logger.LogTrace("Calculating similarity...");
        var results = new List<CompareResult>();
        for (int i = 0; i < mappedImages.Length; i++)
        {
            for (int j = i + 1; j < mappedImages.Length; j++)
            {
                var similarity = GetSimilarityRatio(mappedImages[i], mappedImages[j]);
                var compareResult = new CompareResult(mappedImages[i], mappedImages[j], similarity);
                results.Add(compareResult);
            }
        }
        
        logger.LogTrace("Found {Count} pairs of images with similarity. Filtering results...", results.Count);
        var duplicateResults = results
            .OrderByDescending(r => r.Similarity)
            .Where(t => t.Similarity * 100 >= similarityBar);

        foreach (var duplicatePair in duplicateResults)
        {
            logger.LogInformation($"Found duplicate pair: {duplicatePair.Left} and {duplicatePair.Right} with similarity {duplicatePair.Similarity * 100}%.");
        }
    }

    private double GetSimilarityRatio(MappedImage left, MappedImage right)
    {
        var similarity = GetSimilarity(left, right);
        return (64 - similarity) / 64.0;
    }
    
    private int GetSimilarity(MappedImage left, MappedImage right)
    {
        return BitOperations.PopCount(left.Hash ^ right.Hash);
    }
}