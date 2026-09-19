using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aiursoft.NiBot.Archive;

/// <summary>Content-addressed and isolated by the model AND preprocessing fingerprint.</summary>
public sealed class EmbeddingCache(string directory, IImageEmbeddingProvider provider)
{
    public float[] Get(IndexedPhoto photo)
    {
        if (ArchiveFiles.Hash(photo.Path) != photo.Sha256)
            throw new IOException($"Image changed during indexing: {photo.Path}");
        var modelKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(provider.Fingerprint)));
        var path = Path.Combine(directory, modelKey, photo.Sha256 + ".json");
        ArchiveFiles.AssertNoLinks(path);
        if (File.Exists(path))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path));
                if (entry?.Fingerprint == provider.Fingerprint && entry.Sha256 == photo.Sha256)
                    return CategoryMatcher.Normalize(entry.Vector);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                // An interrupted or invalid cache is expendable; recompute from the source image.
            }
        }

        var vector = CategoryMatcher.Normalize(provider.Embed(photo.Path));
        if (ArchiveFiles.Hash(photo.Path) != photo.Sha256)
            throw new IOException($"Image changed during embedding: {photo.Path}");
        ArchiveFiles.WriteJson(path, new CacheEntry(provider.Fingerprint, photo.Sha256, vector));
        return vector;
    }

    private sealed record CacheEntry(string Fingerprint, string Sha256, float[] Vector);
}
