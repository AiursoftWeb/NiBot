using System.Diagnostics;
using System.Runtime.InteropServices;
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
            .Reverse()
            .Take(200)
            .ToArray();

        logger.LogTrace("Found {Count} images in {path}. Calculating hashes...", images.Length, path);
        var mappedImages = await imageHasher.MapImagesAsync(images);
        
        logger.LogTrace("Calculating similarity...");
        var results = mappedImages.YieldPairs()
            .Select(pair => new CompareResult(pair.left, pair.right))
            .OrderByDescending(r => r.Similarity)
            .Where(t => t.Similarity * 100 >= similarityBar);

        foreach (var duplicatePair in results)
        {
            logger.LogInformation($"Found duplicate pair: {duplicatePair.Left} and {duplicatePair.Right} with similarity {duplicatePair.Similarity * 100}%.");
            var shouldTakeAction = true;
            if (interactive) // In interactive mode, ask for user input.
            {
                PreviewImage(duplicatePair.Left.PhysicalPath);
                PreviewImage(duplicatePair.Right.PhysicalPath);
                while (true)
                {
                    Console.WriteLine("Do you want to delete one of them? (y/n)");
                    var key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Y)
                    {
                        shouldTakeAction = true;
                        break;
                    }
                    if (key.Key == ConsoleKey.N)
                    {
                        shouldTakeAction = false;
                        break;
                    }
                }
            }

            if (shouldTakeAction)
            {
                switch (action)
                {
                    case DuplicateAction.Nothing:
                        logger.LogInformation("No action taken.");
                        break;
                    case DuplicateAction.Delete:
                        logger.LogInformation("Deleted {path}.", duplicatePair.Right.PhysicalPath);
                        break;
                    case DuplicateAction.MoveToTrash:
                        logger.LogInformation("Moved {path} to trash.", duplicatePair.Right.PhysicalPath);
                        break;
                }
            }
        }
    }

    private void PreviewImage(string path)
    {
        // Open the image in a window.
        try
        {
            // If Windows:
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", path);
            }
            // If Linux:
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", path);
            }
            // If macOS:
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", path);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to open image {path}.", path);
        }
    }
}