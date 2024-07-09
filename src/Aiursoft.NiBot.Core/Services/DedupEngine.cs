﻿using System.Diagnostics;
using System.Runtime.InteropServices;
using Aiursoft.NiBot.Core.Models;
using Aiursoft.NiBot.Core.Util;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NiBot.Core.Services;

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
        if (verbose)
        {
            logger.LogInformation(
                "Start de-duplicating images in {path}. Minimum similarity bar is {similarityBar}. Recursive: {recursive}. Keep: {keepPreferences}. Action: {action}. Interactive: {interactive}. Extensions: {extensions}.",
                path, similarityBar, recursive, keepPreferences, action, interactive, string.Join(", ", extensions));
        }
        else
        {
            logger.LogInformation("Start de-duplicating images in {path}. ", path);
        }

        var files = Directory.GetFiles(path, "*.*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false) // Ignore .trash folder.
            .Where(f => !new FileInfo(f).Attributes.HasFlag(FileAttributes.ReparsePoint)); // Ignore symbolic links.
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
            var bestPhoto = FindBestPhoto(group, keepPreferences);
            logger.LogInformation("Found {Count} duplicates pictures with image {Best}", group.Length - 1,
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
                        photo.IsGrayscale, photo.Resolution, photo.Size, photo.PhysicalPath,
                        photo.ImageSimilarityRatio(bestPhoto) * 100 + "%",
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
                        break;
                    case DuplicateAction.Nothing:
                        logger.LogWarning(
                            "No action taken. If you want to delete or move the duplicate photos, please specify the action with --action.");
                        break;
                    case DuplicateAction.MoveToTrashAndCreateLink:
                        await MoveToTrashAsync(photo, path, bestPhoto.PhysicalPath);
                        CreateLink(bestPhoto.PhysicalPath, photo.PhysicalPath);
                        break;
                    case DuplicateAction.DeleteAndCreateLink:
                        File.Delete(photo.PhysicalPath);
                        logger.LogInformation("Deleted {path}.", photo.PhysicalPath);
                        CreateLink(bestPhoto.PhysicalPath, photo.PhysicalPath);
                        logger.LogInformation("Deleted {path} and created a link.", photo.PhysicalPath);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action), action, null);
                }
            }
        }
    }

    public async Task DedupCopyAsync(
        string sourceFolder,
        string destinationFolder,
        int similarityBar,
        bool recursive,
        KeepPreference[] keepPreferences,
        bool interactive,
        string[] extensions,
        bool verbose, int threads)
    {
        if (verbose)
        {
            logger.LogInformation(
                "Start copying without duplicate images in {sourceFolder}. Minimum similarity bar is {similarityBar}. Recursive: {recursive}. Action: Copy. Interactive: {interactive}. Extensions: {extensions}.",
                sourceFolder, similarityBar, recursive, interactive, string.Join(", ", extensions));
        }
        else
        {
            logger.LogInformation("Start copying without duplicate images in {sourceFolder}.", sourceFolder);
        }

        // TODO: Add a new UT to test the feature of the recursive option.
        var sourceFiles = Directory.GetFiles(sourceFolder, "*.*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false); // Ignore .trash folder.
        var sourceImages = sourceFiles
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        
        logger.LogInformation("Found {Count} images in {sourceFolder}. Calculating hashes...", sourceImages.Length, sourceFolder);
        var sourceMappedImages = await imageHasher.MapImagesAsync(sourceImages, showProgress: !verbose, threads: threads);

        var destinationFiles = Directory.GetFiles(destinationFolder, "*.*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false); // Ignore .trash folder.
        var destinationImages = destinationFiles
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        logger.LogInformation("Found {Count} images in {destinationFolder}. Calculating hashes...", destinationImages.Length, destinationFolder);
        var destinationMappedImages = await imageHasher.MapImagesAsync(destinationImages.ToArray(), showProgress: !verbose, threads: threads);
        
        logger.LogInformation("Calculating duplicates...");
        var imageGroups = BuildImageGroups(sourceMappedImages, similarityBar, ignoreSingletons: false).ToArray();
        logger.LogInformation("Found {Count} duplicate groups and totally {Total} duplicate pictures in source folder.",
            imageGroups.Length, imageGroups.Sum(t => t.Length));
        
        logger.LogInformation("Copying images...");
        var maxDistance =
            64 - (int)Math.Round(64 * similarityBar / 100.0) + 1; // VPTree search doesn't cover the upper bound, so +1.
        var destinationImageTree = new VpTree<MappedImage>((MappedImage[])destinationMappedImages.Clone(), (x, y) => x.ImageDiff(y));

        var copiedImagesCount = 0;
        var skippedImagesCount = 0;
        foreach (var group in imageGroups)
        {
            // TODO: Add a new UT to test the case that the best photo is valid.
            var bestPhoto = FindBestPhoto(group, keepPreferences);
            var duplicate = destinationImageTree.SearchByMaxDist(bestPhoto, maxDistance).Select(t => t.Item1).FirstOrDefault();
            if (duplicate != null)
            {
                Interlocked.Increment(ref skippedImagesCount);
                logger.LogInformation("Found a source image {sourceImage} is a duplicate of {duplicate}. Will skip copying.", bestPhoto.PhysicalPath, duplicate.PhysicalPath);
            }
            else
            {
                // Copy the file.
                logger.LogInformation("Copying {sourceImage} to {destinationFolder}.", bestPhoto.PhysicalPath, destinationFolder);

                var sourceFilePath = bestPhoto.PhysicalPath;
                while (IsSymbolicLink(sourceFilePath!))
                {
                    // TODO: Add a new UT to test the case that the source file is a symbolic link.
                    sourceFilePath = new FileInfo(sourceFilePath!).ResolveLinkTarget(true)?.FullName;
                }
                
                // TODO: Add a new UT to test the case that the destination file already exists.
                // TODO: Add a new UT to test the case that the destination file is not under root of the destination folder.
                var destinationPath = Path.Combine(destinationFolder, Path.GetRelativePath(sourceFolder, bestPhoto.PhysicalPath));
                while (File.Exists(destinationPath))
                {
                    logger.LogWarning("File {destinationPath} already exists. Will rename the file.", destinationPath);
                    destinationPath = Path.Combine(destinationFolder, $"{Guid.NewGuid()}{Path.GetExtension(bestPhoto.PhysicalPath)}");
                }
                
                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory!);
                }
                File.Copy(sourceFilePath!, destinationPath);
                Interlocked.Increment(ref copiedImagesCount);
            }
        }
        
        logger.LogInformation("In the source path there are {Count} images, groupped with {GroupCount} groups. {CopiedCount} images are copied and {SkippedCount} images are skipped.",
            sourceMappedImages.Length, imageGroups.Length, copiedImagesCount, skippedImagesCount);
    }
    
    private void CreateLink(string actualFile, string virtualFile)
    {
        var virtualFilePathWithoutFileName = Path.GetDirectoryName(virtualFile)!;
        var relativeActualFile = Path.GetRelativePath(virtualFilePathWithoutFileName, actualFile);
        File.CreateSymbolicLink(virtualFile, relativeActualFile);

        if (File.Exists(virtualFile)
            && new FileInfo(virtualFile).Attributes.HasFlag(FileAttributes.ReparsePoint)
            && new FileInfo(virtualFile).ResolveLinkTarget(true)?.FullName == actualFile)
        {
            logger.LogInformation("Created a link from {virtualFile} to {actualFile}.", virtualFile, actualFile);
        }
        else
        {
            var message = $"Failed to create a link from {virtualFile} to {actualFile}. " +
                          $"Detailed information: " +
                          $"Virtual File: {virtualFile}, " +
                          $"Actual File: {actualFile}, " +
                          $"Relative Actual File: {relativeActualFile}." +
                          $"Virtual File Exists: {File.Exists(virtualFile)}, " +
                          $"Virtual File Size: {new FileInfo(virtualFile).Length}, " +
                          $"Virtual File is a link: {new FileInfo(virtualFile).Attributes.HasFlag(FileAttributes.ReparsePoint)}, " +
                          $"Virtual File Target: {new FileInfo(virtualFile).ResolveLinkTarget(true)?.FullName}.";
            logger.LogError(message);
            throw new Exception(message);
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

        logger.LogInformation("Moved {path} to .trash folder.", photo.PhysicalPath);
    }

    private IEnumerable<MappedImage[]> BuildImageGroups(MappedImage[] mappedImages, int similarityBar, bool ignoreSingletons = true)
    {
        var maxDistance =
            64 - (int)Math.Round(64 * similarityBar / 100.0) + 1; // VPTree search doesn't cover the upper bound, so +1.
        var imageTree = new VpTree<MappedImage>((MappedImage[])mappedImages.Clone(), (x, y) => x.ImageDiff(y));
        var dsu = new DisjointSetUnion(mappedImages.Length);
        foreach (var item in mappedImages)
        {
            var matches = imageTree.SearchByMaxDist(item, maxDistance);
            foreach (var match in matches)
            {
                dsu.Union(item.Id, match.Item1.Id);
            }
        }

        return dsu.AsGroups(ignoreSingletons)
            .Select(groupImgIds => groupImgIds.Select(groupImgId => mappedImages[groupImgId]).ToArray());
    }
    
    private bool IsSymbolicLink(string path)
    {
        return new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint);
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
    
    private MappedImage FindBestPhoto(IEnumerable<MappedImage> group, KeepPreference[] keepPreferences)
    {
        var query = group.OrderByDescending(ConvertKeepPreferenceToExpression.Convert(keepPreferences.First()));
        query = keepPreferences.Skip(1).Aggregate(query,
            (current, keepPreference) =>
                current.ThenByDescending(ConvertKeepPreferenceToExpression.Convert(keepPreference)));
        var bestPhoto = query.First();
        return bestPhoto;
    }
}