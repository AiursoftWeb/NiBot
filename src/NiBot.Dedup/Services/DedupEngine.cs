using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NiBot.Dedup.Models;
using NiBot.Dedup.Util;

namespace NiBot.Dedup.Services;

public class DedupEngine(ILogger<DedupEngine> logger, ImageHasher imageHasher)
{
    public async Task DedupAsync(string path, int similarityBar, bool recursive, KeepPreference[] keepPreferences, DuplicateAction action, bool interactive, string[] extensions, bool verbose)
    {
        logger.LogTrace("Start de-duplicating images in {path}. Minimum similarity bar is {similarityBar}. Recursive: {recursive}. Keep: {keepPreferences}. Action: {action}. Interactive: {interactive}. Extensions: {extensions}.",
            path, similarityBar, recursive, keepPreferences, action, interactive, string.Join(", ", extensions));
        var files = Directory.GetFiles(path, "*.*",
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        var images = files
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        logger.LogInformation("Found {Count} images in {path}. Calculating hashes...", images.Length, path);
        var mappedImages = await imageHasher.MapImagesAsync(images, showProgress: !verbose);
        
        logger.LogInformation("Calculating duplicates...");
        var imageGroups = BuildImageGroups(mappedImages, similarityBar).ToArray();
        logger.LogInformation("Found {Count} duplicate groups and totally {Total} duplicate pictures.", imageGroups.Length, imageGroups.Sum(t => t.Length));

        foreach (var group in imageGroups)
        {
            // TODO: Move this to a helper class for getting best photo.
            var query = group.OrderByDescending(ConvertKeepPreferenceToExpression.Convert(keepPreferences.First()));
            query = keepPreferences.Skip(1).Aggregate(query, (current, keepPreference) => current.ThenByDescending(ConvertKeepPreferenceToExpression.Convert(keepPreference)));
            var bestPhoto = query.First();
            
            logger.LogInformation("Found {Count} duplicates pictures.", group.Length);
            PreviewImage(bestPhoto.PhysicalPath);
            
            logger.LogInformation("Previewing the best photo {path}. Grayscale {grayscale}. Press any key to continue.", bestPhoto.PhysicalPath, bestPhoto.IsGrayscale);
            Console.ReadKey();
            foreach (var photo in group.Where(photo => photo != bestPhoto))
            {
                logger.LogInformation("Previewing the duplicate photo {path}. Grayscale {grayscale}. Press any key to continue.", photo.PhysicalPath, photo.IsGrayscale);
                PreviewImage(photo.PhysicalPath);
                Console.ReadKey();
                //File.Delete(photo.PhysicalPath);
            }
        }
        
        // var results = mappedImages.YieldPairs()
        //     .Select(pair => new CompareResult(pair.left, pair.right))
        //     .OrderByDescending(r => r.Similarity)
        //     .Where(t => t.Similarity * 100 >= similarityBar);
        //
        // foreach (var duplicatePair in results)
        // {
        //     logger.LogInformation($"Found duplicate pair: {duplicatePair.Left} and {duplicatePair.Right} with similarity {duplicatePair.Similarity * 100}%.");
        //     var shouldTakeAction = true;
        //     if (interactive) // In interactive mode, ask for user input.
        //     {
        //         PreviewImage(duplicatePair.Left.PhysicalPath);
        //         PreviewImage(duplicatePair.Right.PhysicalPath);
        //         while (true)
        //         {
        //             Console.WriteLine("Do you want to delete one of them? (y/n)");
        //             var key = Console.ReadKey();
        //             if (key.Key == ConsoleKey.Y)
        //             {
        //                 shouldTakeAction = true;
        //                 break;
        //             }
        //             if (key.Key == ConsoleKey.N)
        //             {
        //                 shouldTakeAction = false;
        //                 break;
        //             }
        //         }
        //     }
        //     
        //     if (shouldTakeAction)
        //     {
        //         // Respect the keepPreferences argument.
        //         // var thePhotoShouldBeTakenAction = keepPreferences switch
        //         // {
        //         //     KeepPreference.Newest => duplicatePair.Left.CreationTime < duplicatePair.Right.CreationTime,
        //         //     KeepPreference.Oldest => duplicatePair.Left.CreationTime > duplicatePair.Right.CreationTime,
        //         //     KeepPreference.Smallest => duplicatePair.Left.Length > duplicatePair.Right.Length,
        //         //     KeepPreference.Largest => duplicatePair.Left.Length < duplicatePair.Right.Length,
        //         //     KeepPreference.HighestResolution => duplicatePair.Left.Width * duplicatePair.Left.Height < duplicatePair.Right.Width * duplicatePair.Right.Height,
        //         //     KeepPreference.LowestResolution => duplicatePair.Left.Width * duplicatePair.Left.Height > duplicatePair.Right.Width * duplicatePair.Right.Height,
        //         //     _ => throw new ArgumentOutOfRangeException()
        //         // };
        //         
        //         switch (action)
        //         {
        //             case DuplicateAction.Nothing:
        //                 logger.LogInformation("No action taken.");
        //                 break;
        //             case DuplicateAction.Delete:
        //                 logger.LogInformation("Deleted {path}.", duplicatePair.Right.PhysicalPath);
        //                 break;
        //             case DuplicateAction.MoveToTrash:
        //                 var trashFolder = Path.Combine(path, ".trash");
        //                 if (!Directory.Exists(trashFolder))
        //                 {
        //                     Directory.CreateDirectory(trashFolder);
        //                 }
        //                 var trashPath = Path.Combine(trashFolder, Path.GetFileName(duplicatePair.Right.PhysicalPath));
        //                 File.Move(duplicatePair.Right.PhysicalPath, trashPath);
        //                 logger.LogInformation("Moved {path} to trash.", duplicatePair.Right.PhysicalPath);
        //                 break;
        //         }
        //     }
        // }
    }
    
    private IEnumerable<MappedImage[]> BuildImageGroups(MappedImage[] mappedImages, int similarityBar)
    {
        var maxDistance = 64 - (int)Math.Round(64 * similarityBar / 100.0) + 1; // VPTree search doesn't cover the upper bound, so +1.
        var imageTree = new VpTree<MappedImage>((MappedImage[])mappedImages.Clone(), (x, y) => x.ImageDiff(y));
        var dsu = new DisjointSetUnion(mappedImages.Length);
        foreach (var item in mappedImages)
        {
            var matches = imageTree.SearchByMaxDist(item, maxDistance);
            matches.ForEach(t => dsu.Union(item.Id, t.Item1.Id));
        }

        return dsu.AsGroups(true)
            .Select(groupImgIds => groupImgIds.Select(groupImgId => mappedImages[groupImgId]).ToArray());
    }

    // ReSharper disable once UnusedMember.Local
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