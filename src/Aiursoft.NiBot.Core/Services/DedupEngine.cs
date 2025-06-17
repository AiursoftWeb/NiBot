using Aiursoft.NiBot.Core.Models;
using Aiursoft.NiBot.Core.Util;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NiBot.Core.Services;

public class DedupEngine(
    ILogger<DedupEngine> logger,
    BestPhotoSelector bestPhotoSelector,
    FilesHelper filesHelper)
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

        var mappedImages = await filesHelper.GetPhotosUnderPath(
            path: path,
            recursive: recursive,
            extensions: extensions,
            includeSymbolicLinks: false, // We are dedup to save space. Symbolic links are not considered as duplicates.
            verbose: verbose,
            threads: threads);

        var imageGroups = BuildImageGroups(mappedImages, similarityBar);

        foreach (var group in imageGroups)
        {
            var bestPhoto = bestPhotoSelector.FindBestPhoto(group, keepPreferences);
            logger.LogInformation("Found {Count} duplicates pictures with image {Best}", group.Length - 1,
                bestPhoto.PhysicalPath);

            if (interactive)
            {
                filesHelper.PreviewImage(bestPhoto.PhysicalPath);

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
                    filesHelper.PreviewImage(photo.PhysicalPath);
                    Console.ReadLine();
                }

                switch (action)
                {
                    case DuplicateAction.Delete:
                        File.Delete(photo.PhysicalPath);
                        logger.LogInformation("Deleted {path}.", photo.PhysicalPath);
                        break;
                    case DuplicateAction.MoveToTrash:
                        await filesHelper.MoveToTrashAsync(photo, path, bestPhoto.PhysicalPath);
                        break;
                    case DuplicateAction.Nothing:
                        logger.LogWarning(
                            "No action taken. If you want to delete or move the duplicate photos, please specify the action with --action.");
                        break;
                    case DuplicateAction.MoveToTrashAndCreateLink:
                        await filesHelper.MoveToTrashAsync(photo, path, bestPhoto.PhysicalPath);
                        filesHelper.CreateLink(bestPhoto.PhysicalPath, photo.PhysicalPath);
                        break;
                    case DuplicateAction.DeleteAndCreateLink:
                        File.Delete(photo.PhysicalPath);
                        logger.LogInformation("Deleted {path}.", photo.PhysicalPath);
                        filesHelper.CreateLink(bestPhoto.PhysicalPath, photo.PhysicalPath);
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
        var sourceMappedImages = await filesHelper.GetPhotosUnderPath(
            path: sourceFolder,
            recursive: recursive,
            extensions: extensions,
            includeSymbolicLinks: true, // We want to copy the symbolic links' actual files to the destination folder.
            verbose: verbose,
            threads: threads);

        var destinationMappedImages = await filesHelper.GetPhotosUnderPath(
            path: destinationFolder,
            recursive: recursive,
            extensions: extensions,
            includeSymbolicLinks: false, // If we copied a file and caused duplicate with a symbolic link, it's fine. So don't index symbolic links in destination folder.
            verbose: verbose,
            threads: threads);

        var sourceImageGroups = BuildImageGroups(sourceMappedImages, similarityBar, ignoreSingletons: false);

        logger.LogInformation("Copying images...");
        var maxDistance =
            64 - (int)Math.Round(64 * similarityBar / 100.0) + 1; // VPTree search doesn't cover the upper bound, so +1.
        var destinationImageTree =
            new VpTree<MappedImage>((MappedImage[])destinationMappedImages.Clone(), (x, y) => x.ImageDiff(y));

        var copiedImagesCount = 0;
        var skippedImagesCount = 0;
        foreach (var group in sourceImageGroups)
        {
            // TODO: Add a new UT to test the case that the best photo is valid.
            var bestPhoto = bestPhotoSelector.FindBestPhoto(group, keepPreferences);
            var duplicate = destinationImageTree.SearchByMaxDist(bestPhoto, maxDistance).Select(t => t.Item1)
                .FirstOrDefault();
            if (duplicate != null)
            {
                Interlocked.Increment(ref skippedImagesCount);
                logger.LogTrace(
                    "Found a source image {sourceImage} is a duplicate of {duplicate}. Will skip copying.",
                    bestPhoto.PhysicalPath, duplicate.PhysicalPath);
                if (interactive)
                {
                    filesHelper.PreviewImage(bestPhoto.PhysicalPath);
                    filesHelper.PreviewImage(duplicate.PhysicalPath);
                    logger.LogInformation("Press ENTER to continue.");
                    Console.ReadLine();
                }
            }
            else
            {
                // Copy the file.
                logger.LogInformation("Copying {sourceImage} to {destinationFolder}.", bestPhoto.PhysicalPath,
                    destinationFolder);

                var sourceFilePath = FilesHelper.GetActualFilePath(bestPhoto.PhysicalPath);
                var destinationPath = Path.Combine(destinationFolder, Path.GetRelativePath(sourceFolder, bestPhoto.PhysicalPath));
                while (File.Exists(destinationPath))
                {
                    logger.LogWarning("File {destinationPath} already exists. Will rename the file.", destinationPath);
                    destinationPath = Path.Combine(destinationFolder,
                        $"{Guid.NewGuid()}{Path.GetExtension(bestPhoto.PhysicalPath)}");
                }

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory!);
                }

                if (interactive)
                {
                    filesHelper.PreviewImage(bestPhoto.PhysicalPath);
                    logger.LogInformation("Press ENTER to copy the file from {sourcePath} to {destinationPath}.",
                        sourceFilePath, destinationPath);
                    Console.ReadLine();
                }

                File.Copy(sourceFilePath, destinationPath);
                Interlocked.Increment(ref copiedImagesCount);
            }
        }

        logger.LogInformation(
            "In the source path there are {Count} images, grouped with {GroupCount} groups. {CopiedCount} images are copied and {SkippedCount} images are skipped.",
            sourceMappedImages.Length, sourceImageGroups.Length, copiedImagesCount, skippedImagesCount);
    }

    /// <summary>
    /// Dedup patch will fetch all files from source directory and destination directory, and if a picture in source directory is a duplicate of a picture in destination directory, and source picture has a higher quality, then the source picture will be patched to the destination picture.
    ///
    /// This action won't dedup the source directory or the destination directory.
    /// </summary>
    public async Task DedupPatchAsync(
        string sourceFolder,
        string destinationFolder,
        bool recursive,
        int similarityBar,
        KeepPreference[] keepPreferences,
        string[] extensions,
        bool verbose, int threads)
    {
        if (verbose)
        {
            logger.LogInformation(
                "Start patching images in {sourceFolder} to {destinationFolder}. Minimum similarity bar is {similarityBar}. Extensions: {extensions}.",
                sourceFolder, destinationFolder, similarityBar, string.Join(", ", extensions));
        }
        else
        {
            logger.LogInformation("Start patching images in {sourceFolder} to {destinationFolder}.", sourceFolder,
                destinationFolder);
        }

        var sourceFiles = await filesHelper.GetPhotosUnderPath(
            path: sourceFolder,
            recursive: recursive,
            extensions: extensions,
            includeSymbolicLinks: true, // We want to copy the symbolic links' actual files to the destination folder.
            verbose: verbose,
            threads: threads);
        var destinationFiles = await filesHelper.GetPhotosUnderPath(
            path: destinationFolder,
            recursive: recursive,
            extensions: extensions,
            includeSymbolicLinks: false, // We are patching destination files, so we don't want to patch the symbolic links.
            verbose: verbose,
            threads: threads);
        var mappedImages = sourceFiles.Concat(destinationFiles).ToArray();

        var imageGroups = BuildImageGroups(mappedImages, similarityBar).ToArray();
        logger.LogInformation(
            "Found {Count} duplicate groups and totally {Total} duplicate pictures in source and destination folders.",
            imageGroups.Length, imageGroups.Sum(t => t.Length));
        logger.LogInformation("Patching images...");

        var patchedImagesCount = 0;
        foreach (var images in imageGroups)
        {
            var bestPhoto = bestPhotoSelector.FindBestPhoto(images, keepPreferences);
            if (bestPhoto.PhysicalPath.StartsWith(sourceFolder, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var destImage in images.Where(img => img.PhysicalPath.StartsWith(destinationFolder, StringComparison.OrdinalIgnoreCase)))
                {
                    logger.LogInformation(
                        "Patching {source} -> {dest}",
                        bestPhoto.PhysicalPath,
                        destImage.PhysicalPath
                    );
                    File.Copy(
                        bestPhoto.PhysicalPath,
                        destImage.PhysicalPath,
                        overwrite: true
                    );
                    Interlocked.Increment(ref patchedImagesCount);
                }
            }
        }

        logger.LogInformation(
            "In the source path there are {Count} images, grouped with {GroupCount} groups. {PatchedCount} images are patched.",
            mappedImages.Length, imageGroups.Length, patchedImagesCount);
    }

    private MappedImage[][] BuildImageGroups(MappedImage[] mappedImages, int similarityBar,
        bool ignoreSingletons = true)
    {
        logger.LogInformation("Calculating duplicates for {Count} images with similarity bar {SimilarityBar}.",
            mappedImages.Length, similarityBar);
        logger.LogTrace("Ignore singletons means if a group has only one image, it will be ignored: {IgnoreSingletons}.",
            ignoreSingletons);

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

        var imageGroups = dsu.AsGroups(ignoreSingletons)
            .Select(groupImgIds =>
                groupImgIds
                    .Select(groupImgId => mappedImages[groupImgId])
                    .ToArray()
            ).ToArray();
        logger.LogInformation("Found {Count} duplicate groups and totally {Total} duplicate pictures.",
            imageGroups.Length, imageGroups.Sum(t => t.Length));
        return imageGroups;
    }
}
