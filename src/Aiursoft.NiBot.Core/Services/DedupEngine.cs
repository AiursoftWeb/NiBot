﻿using Aiursoft.NiBot.Core.Models;
using Aiursoft.NiBot.Core.Util;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NiBot.Core.Services;

public class DedupEngine(
    ILogger<DedupEngine> logger,
    ImageHasher imageHasher,
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

        var files = Directory.GetFiles(path, "*.*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false) // Ignore .trash folder.
            .Where(f => !filesHelper
                .IsSymbolicLink(
                    f)); // Ignore symbolic links. Because these symbolic links may point to the same file, but deduplication should not consider them as duplicates.
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

        // TODO: Add a new UT to test the feature of the recursive option.
        var sourceFiles = Directory.GetFiles(sourceFolder, "*.*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false); // Ignore .trash folder.
        // This time we don't ignore symbolic links. Because we want to copy the symbolic links' actual files to the destination folder.

        var sourceImages = sourceFiles
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        logger.LogInformation("Found {Count} images in {sourceFolder}. Calculating hashes...", sourceImages.Length,
            sourceFolder);
        var sourceMappedImages =
            await imageHasher.MapImagesAsync(sourceImages, showProgress: !verbose, threads: threads);

        var destinationFiles = Directory.GetFiles(destinationFolder, "*.*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false); // Ignore .trash folder.
        var destinationImages = destinationFiles
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        logger.LogInformation("Found {Count} images in {destinationFolder}. Calculating hashes...",
            destinationImages.Length, destinationFolder);
        var destinationMappedImages =
            await imageHasher.MapImagesAsync(destinationImages.ToArray(), showProgress: !verbose, threads: threads);

        logger.LogInformation("Calculating duplicates...");
        var imageGroups = BuildImageGroups(sourceMappedImages, similarityBar, ignoreSingletons: false).ToArray();
        logger.LogInformation("Found {Count} duplicate groups and totally {Total} duplicate pictures in source folder.",
            imageGroups.Length, imageGroups.Sum(t => t.Length));

        logger.LogInformation("Copying images...");
        var maxDistance =
            64 - (int)Math.Round(64 * similarityBar / 100.0) + 1; // VPTree search doesn't cover the upper bound, so +1.
        var destinationImageTree =
            new VpTree<MappedImage>((MappedImage[])destinationMappedImages.Clone(), (x, y) => x.ImageDiff(y));

        var copiedImagesCount = 0;
        var skippedImagesCount = 0;
        foreach (var group in imageGroups)
        {
            // TODO: Add a new UT to test the case that the best photo is valid.
            var bestPhoto = bestPhotoSelector.FindBestPhoto(group, keepPreferences);
            var duplicate = destinationImageTree.SearchByMaxDist(bestPhoto, maxDistance).Select(t => t.Item1)
                .FirstOrDefault();
            if (duplicate != null)
            {
                Interlocked.Increment(ref skippedImagesCount);
                logger.LogInformation(
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

                var sourceFilePath = bestPhoto.PhysicalPath;
                while (filesHelper.IsSymbolicLink(sourceFilePath!))
                {
                    // TODO: Add a new UT to test the case that the source file is a symbolic link.
                    sourceFilePath = new FileInfo(sourceFilePath!).ResolveLinkTarget(true)?.FullName;
                }

                // TODO: Add a new UT to test the case that the destination file already exists.
                // TODO: Add a new UT to test the case that the destination file is not under root of the destination folder.
                var destinationPath = Path.Combine(destinationFolder,
                    Path.GetRelativePath(sourceFolder, bestPhoto.PhysicalPath));
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

                File.Copy(sourceFilePath!, destinationPath);
                Interlocked.Increment(ref copiedImagesCount);
            }
        }

        logger.LogInformation(
            "In the source path there are {Count} images, grouped with {GroupCount} groups. {CopiedCount} images are copied and {SkippedCount} images are skipped.",
            sourceMappedImages.Length, imageGroups.Length, copiedImagesCount, skippedImagesCount);
    }

    /// <summary>
    /// Dedup patch will fetch all files from source directory and destination directory, and if a picture in source directory is a duplicate of a picture in destination directory, and source picture has a higher quality, then the source picture will be patched to the destination picture.
    ///
    /// This action won't dedup the source directory or the destination directory.
    /// </summary>
    public async Task DedupPatchAsync(
        string sourceFolder,
        string destinationFolder,
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

        var sourceFiles = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false); // Ignore .trash folder.
        // This time we don't ignore symbolic links. Because we want to copy the symbolic links' actual files to the destination folder.
        var sourceImages = sourceFiles
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var destinationFiles = Directory.GetFiles(destinationFolder, "*.*", SearchOption.AllDirectories)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false) // Ignore .trash folder.
            .Where(f => !filesHelper
                .IsSymbolicLink(f)); // Ignore symbolic links. Because these symbolic links may point to the same file.
        var destinationImages = destinationFiles
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var mergedImages = sourceImages.Concat(destinationImages).ToArray();

        logger.LogInformation("Calculating duplicates...");
        var mappedImages = await imageHasher.MapImagesAsync(mergedImages, showProgress: !verbose, threads: threads);

        var imageGroups = BuildImageGroups(mappedImages, similarityBar).ToArray();
        logger.LogInformation(
            "Found {Count} duplicate groups and totally {Total} duplicate pictures in source and destination folders.",
            imageGroups.Length, imageGroups.Sum(t => t.Length));
        logger.LogInformation("Patching images...");

        var patchedImagesCount = 0;
        foreach (var images in imageGroups)
        {
            var bestPhoto = bestPhotoSelector.FindBestPhoto(images, keepPreferences);

            // If best photo is in source, and there is a duplicate in destination, then patch the source to the destination.
            if (mappedImages.Contains(bestPhoto))
            {
                foreach (var destinationImage in images.Where(i => i != bestPhoto))
                {
                    if (destinationImages.Any(di => di == destinationImage.PhysicalPath))
                    {
                        // Patch the source to the destination.
                        logger.LogInformation("Patching {sourceImage} to {destinationImage}.", bestPhoto.PhysicalPath,
                            destinationImage.PhysicalPath);
                        File.Copy(bestPhoto.PhysicalPath, destinationImage.PhysicalPath, true);
                        Interlocked.Increment(ref patchedImagesCount);
                    }
                }
            }
        }

        logger.LogInformation(
            "In the source path there are {Count} images, grouped with {GroupCount} groups. {PatchedCount} images are patched.",
            mappedImages.Length, imageGroups.Length, patchedImagesCount);
    }

    /// <summary>
    /// Clusters images from a given directory using a K-Means algorithm (based on image hash) and redistributes them into separate group folders.
    /// </summary>
    /// <param name="path">The path of the directory to perform clustering distribution on.</param>
    /// <param name="similarityBar">A parameter (0-100) used to adjust clustering granularity. Higher value means images need to be more similar to fall in the same cluster.</param>
    /// <param name="recursive">A flag indicating whether to process subdirectories recursively.</param>
    /// <param name="interactive">
    /// When true, the program will prompt the user for confirmation before moving images.
    /// </param>
    /// <param name="extensions">
    /// An array of file extensions to consider. Only files with these extensions will be processed.
    /// </param>
    /// <param name="verbose">A flag indicating whether to output verbose log messages.</param>
    /// <param name="threads">The number of threads to use for image hashing.</param>
    /// <returns>A Task representing the asynchronous clustering distribution operation.</returns>
    public async Task ClusterDistributeAsync(
        string path,
        int similarityBar,
        bool recursive,
        bool interactive,
        string[] extensions,
        bool verbose,
        int threads)
    {
        if (verbose)
        {
            logger.LogInformation(
                "Start clustering images in {path}. Similarity parameter: {similarityBar}. Recursive: {recursive}. Extensions: {extensions}.",
                path, similarityBar, recursive, string.Join(", ", extensions));
        }
        else
        {
            logger.LogInformation("Start clustering images in {path}.", path);
        }

        // 获取所有文件（忽略 .trash 文件夹以及符号链接）
        var files = Directory
            .GetFiles(path, "*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false)
            .Where(f => !filesHelper.IsSymbolicLink(f));
        var images = files
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        logger.LogInformation("Found {Count} images in {path}. Calculating hashes...", images.Length, path);
        var mappedImages = await imageHasher.MapImagesAsync(images, showProgress: !verbose, threads: threads);

        int totalImages = mappedImages.Length;
        if (totalImages == 0)
        {
            logger.LogWarning("No images found in the given path.");
            return;
        }

        // 将每个图片的 64 位哈希转换为 64 维二值向量
        var points = new double[totalImages][];
        for (int i = 0; i < totalImages; i++)
        {
            points[i] = ConvertToVector(mappedImages[i]);
        }

        // 根据总图片数和 similarityBar 估算聚类数 k
        int k = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(totalImages) * (similarityBar / 100.0) + 1));
        logger.LogInformation("Clustering into {K} clusters.", k);

        // --- K-Means 算法 ---
        // 随机初始化 k 个中心
        double[][] centers = new double[k][];
        Random rand = new Random();
        var chosenIndices = new HashSet<int>();
        for (int i = 0; i < k; i++)
        {
            int idx;
            do
            {
                idx = rand.Next(totalImages);
            } while (!chosenIndices.Add(idx));

            centers[i] = (double[])points[idx].Clone();
        }

        int[] assignments = new int[totalImages];
        bool changed = true;
        int iterations = 0;
        int maxIterations = 20;

        while (changed && iterations < maxIterations)
        {
            changed = false;
            // 分配阶段：将每个点归入距离最近的中心
            for (int i = 0; i < totalImages; i++)
            {
                double bestDist = double.MaxValue;
                int bestCluster = 0;
                for (int j = 0; j < k; j++)
                {
                    double dist = EuclideanDistanceSquared(points[i], centers[j]);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestCluster = j;
                    }
                }

                if (assignments[i] != bestCluster)
                {
                    assignments[i] = bestCluster;
                    changed = true;
                }
            }

            // 更新阶段：对每个聚类，重新计算中心（取所有向量的均值）
            double[][] newCenters = new double[k][];
            int[] counts = new int[k];
            for (int j = 0; j < k; j++)
            {
                newCenters[j] = new double[64]; // 64 维
            }

            for (int i = 0; i < totalImages; i++)
            {
                int cluster = assignments[i];
                counts[cluster]++;
                for (int d = 0; d < 64; d++)
                {
                    newCenters[cluster][d] += points[i][d];
                }
            }

            for (int j = 0; j < k; j++)
            {
                if (counts[j] > 0)
                {
                    for (int d = 0; d < 64; d++)
                    {
                        newCenters[j][d] /= counts[j];
                    }
                }
                else
                {
                    // 如果某个聚类没有任何点，则随机重置中心
                    int idx = rand.Next(totalImages);
                    newCenters[j] = (double[])points[idx].Clone();
                }
            }

            centers = newCenters;
            iterations++;
        }
        // --- K-Means 算法结束 ---

        // 根据 assignments 构造每个聚类的图片列表
        var clusters = new List<List<MappedImage>>();
        for (int j = 0; j < k; j++)
        {
            clusters.Add(new List<MappedImage>());
        }

        for (int i = 0; i < totalImages; i++)
        {
            clusters[assignments[i]].Add(mappedImages[i]);
        }

        // 移除空的聚类
        clusters = clusters.Where(c => c.Count > 0).ToList();

        int groupCount = clusters.Count;
        double averagePerGroup = groupCount > 0 ? (double)totalImages / groupCount : 0;
        logger.LogInformation("Clustering resulted in {GroupCount} groups, averaging {Average} images per group.",
            groupCount, averagePerGroup);

        if (interactive)
        {
            logger.LogInformation("Press ENTER to confirm moving images into cluster folders.");
            Console.ReadLine();
        }

        // 将每个聚类内的图片移入新建的子文件夹（例如 group-1、group-2 等）
        int groupIndex = 1;
        foreach (var cluster in clusters)
        {
            var groupFolderName = $"group-{groupIndex}";
            var groupFolderPath = Path.Combine(path, groupFolderName);
            if (!Directory.Exists(groupFolderPath))
            {
                Directory.CreateDirectory(groupFolderPath);
            }

            foreach (var image in cluster)
            {
                var sourceFilePath = image.PhysicalPath;
                var fileName = Path.GetFileName(sourceFilePath);
                var destinationPath = Path.Combine(groupFolderPath, fileName);

                // 如果目标文件存在，则生成新文件名
                while (File.Exists(destinationPath))
                {
                    fileName =
                        $"{Path.GetFileNameWithoutExtension(sourceFilePath)}_{Guid.NewGuid()}{Path.GetExtension(sourceFilePath)}";
                    destinationPath = Path.Combine(groupFolderPath, fileName);
                }

                if (interactive)
                {
                    logger.LogInformation(
                        "About to move file {sourceFilePath} to {destinationPath}. Press ENTER to confirm.",
                        sourceFilePath, destinationPath);
                    filesHelper.PreviewImage(sourceFilePath);
                    Console.ReadLine();
                }

                File.Move(sourceFilePath, destinationPath);
                logger.LogInformation("Moved file {sourceFilePath} to {destinationPath}.", sourceFilePath,
                    destinationPath);
            }

            groupIndex++;
        }

        logger.LogInformation(
            "Cluster distribution completed. Total groups: {GroupCount}, Total images processed: {TotalImages}.",
            groupCount, totalImages);

        // --- 本方法中使用的辅助函数 ---
        // 将图片的 Hash 转换为 64 维二值向量
        double[] ConvertToVector(MappedImage image)
        {
            var vector = new double[64];
            // 假设 image.Hash 为 64 位整数（例如 ulong）
            ulong hash = image.Hash;
            for (int i = 0; i < 64; i++)
            {
                vector[i] = ((hash >> i) & 1UL) == 1UL ? 1.0 : 0.0;
            }

            return vector;
        }

        // 计算两个 64 维向量之间的欧式距离平方
        double EuclideanDistanceSquared(double[] a, double[] b)
        {
            double sum = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                double diff = a[i] - b[i];
                sum += diff * diff;
            }

            return sum;
        }
    }

    private IEnumerable<MappedImage[]> BuildImageGroups(MappedImage[] mappedImages, int similarityBar,
        bool ignoreSingletons = true)
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
}