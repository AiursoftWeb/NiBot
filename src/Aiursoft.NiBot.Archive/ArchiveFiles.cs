using System.Security.Cryptography;
using System.Text.Json;
using CoenM.ImageHash.HashAlgorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Aiursoft.NiBot.Archive;

public static class ArchiveFiles
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".jfif", ".webp", ".bmp" };

    public static bool IsImage(string path) => Extensions.Contains(Path.GetExtension(path));

    public static string Root(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException(full);
        AssertNoLinks(full);
        return full;
    }

    public static void AssertDisjoint(string left, string right)
    {
        if (IsWithin(left, right) || IsWithin(right, left))
            throw new ArgumentException("Source and destination directories must not overlap.");
    }

    public static bool IsWithin(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) && relative != ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    public static string Resolve(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new InvalidDataException("Expected a relative path.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!IsWithin(full, root) || full == root)
            throw new InvalidDataException("Path escapes the archive root.");
        AssertNoLinks(full);
        return full;
    }

    public static void AssertNoLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Symbolic links are not supported in archive paths: {current}");
            // LinkTarget also detects dangling links, for which Exists is false.
            if (new FileInfo(current).LinkTarget != null)
                throw new IOException($"Symbolic links are not supported in archive paths: {current}");
        }
    }

    public static IEnumerable<string> Images(string root)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(root).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(entry);
            if (name.StartsWith('.') || (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) continue;
            if (Directory.Exists(entry))
            {
                foreach (var image in Images(entry)) yield return image;
            }
            else if (IsImage(entry)) yield return entry;
        }
    }

    public static string Hash(string path)
    {
        AssertNoLinks(path);
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static IndexedPhoto Index(string root, string path, ArchiveSettings settings, HashSet<string>? imported = null)
    {
        AssertNoLinks(path);
        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        using var image = Image.Load<Rgba32>(bytes);
        var perceptual = new PerceptualHash().Hash(image);
        var relative = Path.GetRelativePath(root, path);
        var parts = relative.Split(Path.DirectorySeparatorChar);
        var category = parts.Length > 1 && !settings.ExcludedCategories.Contains(parts[0], StringComparer.OrdinalIgnoreCase)
                                       && !(imported?.Contains(relative) ?? false) ? parts[0] : null;
        return new IndexedPhoto(path, relative, hash, perceptual, category);
    }

    public static void WriteJson<T>(string path, T value, bool overwrite = true)
    {
        path = Path.GetFullPath(path);
        AssertNoLinks(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, JsonOptions);
                stream.Flush(true);
            }
            File.Move(temporary, path, overwrite);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static HashSet<string> ImportedPaths(string destination)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var journal = Path.Combine(destination, ".nibot-archive", "imports");
        AssertNoLinks(journal);
        if (!Directory.Exists(journal)) return result;
        foreach (var path in Directory.EnumerateFiles(journal, "*.json"))
        {
            AssertNoLinks(path);
            var record = JsonSerializer.Deserialize<ImportRecord>(File.ReadAllText(path))
                         ?? throw new InvalidDataException($"Invalid import record: {path}");
            var imported = Resolve(destination, record.Destination);
            // A stale write-ahead record must not exclude an unrelated file that later
            // occupied the same name after an interrupted/failed import.
            if (File.Exists(imported) && Hash(imported) == record.SourceSha256)
                result.Add(record.Destination);
        }
        return result;
    }
}

public sealed record ImportRecord(string SourceSha256, string Destination);
