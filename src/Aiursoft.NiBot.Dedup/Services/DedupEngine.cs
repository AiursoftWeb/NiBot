﻿using System.Diagnostics;
using System.Runtime.InteropServices;
using Aiursoft.NiBot.Dedup.Models;
using Aiursoft.NiBot.Dedup.Util;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NiBot.Dedup.Services;

public class DedupEngine(ILogger<DedupEngine> logger, ImageHasher imageHasher)
{
    /// <summary>
    /// Removes duplicate files from a given directory.
    /// </summary>
    /// <param name="path">The path of the directory to perform deduplication on.</param>
    /// <param name="similarityBar">The similarity threshold, ranging from 0 to 100, to consider two files duplicates. Suggested value is 96-99.</param>
    /// <param name="recursive">A flag indicating whether to perform deduplication recursively in subdirectories.</param>
    /// <param name="keepPreferences">An array of preferences for deduplication. Specify which files to keep when duplicates are found.</param>
    /// <param name="action">The action to be taken when duplicates are found. Either to delete duplicates or move them to a specified directory.</param>
    /// <param name="interactive">A flag indicating whether to interactively prompt the user for actions on each duplicate set.</param>
    /// <param name="extensions">An array of file extensions to consider for deduplication. If specified, only files with these extensions will be considered.</param>
    /// <param name="verbose">A flag indicating whether to output verbose log messages.</param>
    /// <param name="threads"></param>
    /// <remarks>
    /// The DedupAsync method removes duplicate files from the specified directory based on their content similarity.
    /// Files are considered duplicates if their content similarity percentage is above the specified similarity threshold.
    /// The method supports recursive deduplication in subdirectories and allows customization of deduplication preferences and actions.
    /// </remarks>
    /// <returns>
    /// A Task that represents the asynchronous deduplication operation. The Task completes when the deduplication is finished.
    /// </returns>
    public async Task DedupAsync(string path,
        int similarityBar,
        bool recursive,
        KeepPreference[] keepPreferences,
        DuplicateAction action,
        bool interactive,
        string[] extensions,
        bool verbose, int threads)
    {
        logger.LogTrace(
            "Start de-duplicating images in {path}. Minimum similarity bar is {similarityBar}. Recursive: {recursive}. Keep: {keepPreferences}. Action: {action}. Interactive: {interactive}. Extensions: {extensions}.",
            path, similarityBar, recursive, keepPreferences, action, interactive, string.Join(", ", extensions));
        var files = Directory.GetFiles(path, "*.*",
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        var images = files
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        logger.LogInformation("Found {Count} images in {path}. Calculating hashes...", images.Length, path);
        var mappedImages = await imageHasher.MapImagesAsync(images, showProgress: !verbose, threads: threads);

        logger.LogInformation("Calculating duplicates...");
        var imageGroups = BuildImageGroups(mappedImages, similarityBar).ToArray();
        logger.LogInformation("Found {Count} duplicate groups and totally {Total} duplicate pictures.",
            imageGroups.Length, imageGroups.Sum(t => t.Length));

        foreach (var group in imageGroups)
        {
            // TODO: Move this to a helper class for getting best photo.
            var query = group.OrderByDescending(ConvertKeepPreferenceToExpression.Convert(keepPreferences.First()));
            query = keepPreferences.Skip(1).Aggregate(query,
                (current, keepPreference) =>
                    current.ThenByDescending(ConvertKeepPreferenceToExpression.Convert(keepPreference)));
            var bestPhoto = query.First();

            logger.LogInformation("Found {Count} duplicates pictures with image {Best}", group.Length,
                bestPhoto.PhysicalPath);

            if (interactive)
            {
                PreviewImage(bestPhoto.PhysicalPath);

                logger.LogInformation(
                    "Grayscale {grayscale}. Resolution {resolution}. Size {size}. File name {fileName} is previewing as best photo. Press ENTER to preview duplicates.",
                    bestPhoto.IsGrayscale, bestPhoto.Resolution, bestPhoto.Size, bestPhoto.PhysicalPath);
                Console.ReadLine();
            }

            foreach (var photo in group.Where(photo => photo != bestPhoto))
            {
                if (interactive)
                {
                    logger.LogInformation(
                        "Grayscale {grayscale}. Resolution {resolution}. Size {size}. File name {fileName} is previewing as duplicate photo with similarity {similarity}. Press ENTER to do {action}.",
                        photo.IsGrayscale, photo.Resolution, photo.Size, photo.PhysicalPath, photo.ImageSimilarityRatio(bestPhoto) * 100 + "%",
                        action);
                    PreviewImage(photo.PhysicalPath);
                    Console.ReadLine();
                }

                switch (action)
                {
                    case DuplicateAction.Delete:
                        File.Delete(photo.PhysicalPath);
                        logger.LogInformation("Deleted {path}.", photo.PhysicalPath);
                        break;
                    case DuplicateAction.MoveToTrash:
                        await MoveToTrashAsync(photo, path, bestPhoto.PhysicalPath);
                        logger.LogInformation("Moved {path} to .trash folder.", photo.PhysicalPath);
                        break;
                    case DuplicateAction.Nothing:
                        logger.LogWarning(
                            "No action taken. If you want to delete or move the duplicate photos, please specify the action with --action.");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action), action, null);
                }
            }
        }
    }

    private async Task MoveToTrashAsync(MappedImage photo, string path, string duplicateSourceFile)
    {
        var trashFolder = Path.Combine(path, ".trash");
        if (!Directory.Exists(trashFolder))
        {
            Directory.CreateDirectory(trashFolder);
        }

        var trashPath = Path.Combine(trashFolder, Path.GetFileName(photo.PhysicalPath));
        while (File.Exists(trashPath))
        {
            trashPath = Path.Combine(trashFolder, $"{Guid.NewGuid()}{Path.GetExtension(photo.PhysicalPath)}");
        }

        File.Move(photo.PhysicalPath, trashPath);
        var message =
            $"The file {photo.PhysicalPath} is moved here as {trashPath} because it's a duplicate of {duplicateSourceFile}.";
        var messageFile = Path.Combine(trashFolder, $".duplicateReasons.txt");
        await File.AppendAllTextAsync(messageFile, message + Environment.NewLine);
    }

    private IEnumerable<MappedImage[]> BuildImageGroups(MappedImage[] mappedImages, int similarityBar)
    {
        var maxDistance =
            64 - (int)Math.Round(64 * similarityBar / 100.0) + 1; // VPTree search doesn't cover the upper bound, so +1.
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