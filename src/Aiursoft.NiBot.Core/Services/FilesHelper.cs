using System.Diagnostics;
using System.Runtime.InteropServices;
using Aiursoft.NiBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NiBot.Core.Services;

public class FilesHelper(ILogger<FilesHelper> logger, ImageHasher imageHasher)
{
    public void CreateLink(string actualFile, string virtualFile)
    {
        var virtualFilePathWithoutFileName = Path.GetDirectoryName(virtualFile)!;
        var relativePathToActualFile = Path.GetRelativePath(virtualFilePathWithoutFileName, actualFile);
        File.CreateSymbolicLink(virtualFile, relativePathToActualFile);

        if (File.Exists(virtualFile)
            && IsSymbolicLink(virtualFile)
            && new FileInfo(virtualFile).ResolveLinkTarget(true)?.FullName == Path.GetFullPath(actualFile))
        {
            logger.LogInformation("Created a link from {virtualFile} to {actualFile}.", virtualFile, actualFile);
        }
        else
        {
            var message = $"Failed to create a link from {virtualFile} to {actualFile}. " +
                          $"Detailed information: " +
                          $"Virtual File: {virtualFile}, " +
                          $"Actual File: {actualFile}, " +
                          $"Relative Actual File: {relativePathToActualFile}." +
                          $"Virtual File Exists: {File.Exists(virtualFile)}, " +
                          $"Virtual File Size: {new FileInfo(virtualFile).Length}, " +
                          $"Virtual File is a link: {new FileInfo(virtualFile).Attributes.HasFlag(FileAttributes.ReparsePoint)}, " +
                          $"Virtual File Target: {new FileInfo(virtualFile).ResolveLinkTarget(true)?.FullName}.";
            logger.LogError(message);
            throw new Exception(message);
        }
    }

    public async Task MoveToTrashAsync(MappedImage photo, string path, string duplicateSourceFile)
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

    public static bool IsSymbolicLink(string path)
    {
        //return new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint);
        return new FileInfo(path).LinkTarget != null;
    }

    public void PreviewImage(string path)
    {
        // Open the image in a window.
        try
        {
            // If Windows:
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", $@"""{path}""");
            }
            // If Linux:
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", $@"""{path}""");
            }
            // If macOS:
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $@"""{path}""");
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to open image {path}.", $@"""{path}""");
        }
    }

    public async Task<MappedImage[]> GetPhotosUnderPath(
        string path,
        bool recursive,
        string[] extensions,
        bool includeSymbolicLinks,
        bool verbose,
        int threads)
    {
        // Filter files based on the provided extensions.
        var files = Directory.GetFiles(
                path: path,
                searchPattern: "*.*",
                searchOption: recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false);

        // Filter out symbolic links if not included.
        if (!includeSymbolicLinks)
        {
            files = files.Where(f => !IsSymbolicLink(f));
        }

        // Filter files by extensions.
        var images = files
            .Where(file => extensions.Any(ext =>
                string.Equals(Path.GetExtension(file).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        logger.LogInformation("Found {Count} images in {path}. Calculating hashes...", images.Length, path);
        var mappedImages = await imageHasher.MapImagesAsync(images, showProgress: !verbose, threads: threads);
        return mappedImages;
    }

    public static string GetActualFilePath(string path)
    {
        while (IsSymbolicLink(path))
        {
            path = new FileInfo(path).ResolveLinkTarget(true)?.FullName!;
        }
        return path;
    }
}
