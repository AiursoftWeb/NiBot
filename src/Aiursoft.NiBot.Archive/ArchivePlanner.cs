using System.Numerics;

namespace Aiursoft.NiBot.Archive;

public sealed class ArchivePlanner(IImageEmbeddingProvider provider, string cacheDirectory, Action<string>? log = null)
{
    public ArchivePlan Create(string source, string destination, ArchiveSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        source = ArchiveFiles.Root(source);
        destination = ArchiveFiles.Root(destination);
        ArchiveFiles.AssertDisjoint(source, destination);
        var cacheRoot = Path.GetFullPath(cacheDirectory);
        if (ArchiveFiles.IsWithin(cacheRoot, source) || ArchiveFiles.IsWithin(cacheRoot, destination))
            throw new ArgumentException("Keep the feature cache outside both image directories.");
        var cache = new EmbeddingCache(cacheRoot, provider);
        var imported = ArchiveFiles.ImportedPaths(destination);
        var oldPhotos = new List<IndexedPhoto>();
        foreach (var path in ArchiveFiles.Images(destination))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Do not silently ignore an unreadable reference: it could hide an existing duplicate.
            oldPhotos.Add(ArchiveFiles.Index(destination, path, settings, imported));
            if (oldPhotos.Count % 100 == 0) log?.Invoke($"Indexed {oldPhotos.Count} destination images.");
        }

        var categories = oldPhotos.Where(p => p.Category != null).Select(p => p.Category!)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (categories.Length == 0) throw new InvalidDataException("No reference categories found in destination subdirectories.");
        var plan = new ArchivePlan
        {
            SourceRoot = source, DestinationRoot = destination, Settings = settings,
            ModelFingerprint = provider.Fingerprint, Categories = categories,
            DestinationHashes = oldPhotos.ToDictionary(p => p.RelativePath, p => p.Sha256, StringComparer.Ordinal)
        };

        CategoryMatcher? matcher = null;
        var visualDuplicates = new VisualDuplicateDetector();
        // Deduplication sees earlier source images too, but classification uses only the old reference snapshot.
        var duplicatePool = new List<IndexedPhoto>(oldPhotos);
        var exact = oldPhotos.GroupBy(p => p.Sha256).ToDictionary(g => g.Key, g => g.First());
        foreach (var path in ArchiveFiles.Images(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(source, path);
            IndexedPhoto photo;
            try { photo = ArchiveFiles.Index(source, path, settings); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                plan.Items.Add(new ArchiveItem { Source = relative, Decision = ArchiveDecision.Error, Reason = exception.Message });
                continue;
            }

            var item = new ArchiveItem { Source = relative, Sha256 = photo.Sha256, Decision = ArchiveDecision.Review, Reason = "" };
            if (exact.TryGetValue(photo.Sha256, out var identical))
            {
                plan.Items.Add(item with { Decision = ArchiveDecision.Duplicate, Reason = "Identical SHA-256 content.", DuplicateOf = identical.Path });
                continue;
            }

            var near = duplicatePool.Select(p => (Photo: p, Similarity: (64 - BitOperations.PopCount(p.PerceptualHash ^ photo.PerceptualHash)) * 100.0 / 64))
                .Where(p => p.Similarity >= settings.DuplicateSimilarity).OrderByDescending(p => p.Similarity).FirstOrDefault();
            if (near.Photo != null)
            {
                plan.Items.Add(item with
                {
                    Decision = settings.SkipSimilar ? ArchiveDecision.Duplicate : ArchiveDecision.ReviewDuplicate,
                    Reason = $"Perceptual hash similarity {near.Similarity:F2}%; this is not proof of identical content.",
                    DuplicateOf = near.Photo.Path
                });
                continue;
            }

            // Build embeddings only when at least one new image actually needs classification.
            if (matcher == null)
            {
                var references = new List<ReferencePhoto>();
                foreach (var old in oldPhotos.Where(p => p.Category != null))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    references.Add(new ReferencePhoto(old, cache.Get(old)));
                    if (references.Count % 25 == 0) log?.Invoke($"Embedded {references.Count} reference images.");
                }
                matcher = new CategoryMatcher(references.ToArray());
            }

            try
            {
                var candidates = matcher.Rank(cache.Get(photo), settings.Neighbors);
                var visualMatch = candidates.SelectMany(c => c.Examples).Distinct(StringComparer.Ordinal)
                    .Select(relativePath => ArchiveFiles.Resolve(destination, relativePath))
                    .FirstOrDefault(oldPath => visualDuplicates.IsPossibleDuplicate(photo.Path, oldPath));
                if (visualMatch != null)
                {
                    plan.Items.Add(item with { Decision = ArchiveDecision.ReviewDuplicate,
                        Reason = "Normalized image structure matches a category example; inspect possible crop or color variants.",
                        DuplicateOf = visualMatch, Candidates = candidates });
                    continue;
                }
                var nearest = settings.ClassificationMode == ArchiveClassificationMode.Nearest;
                var confident = nearest || CategoryMatcher.IsConfident(candidates, settings);
                plan.Items.Add(item with
                {
                    Decision = confident ? ArchiveDecision.Ready : ArchiveDecision.Review,
                    Reason = nearest ? "Nearest-category mode: selected the first-ranked category; strict thresholds were not required." : confident ? "Centroid similarity, margin and neighbor support passed." : "Classification is ambiguous or below the configured thresholds.",
                    TargetFolder = confident ? candidates[0].Folder : null, Candidates = candidates
                });
                duplicatePool.Add(photo);
                exact.TryAdd(photo.Sha256, photo);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                plan.Items.Add(item with { Decision = ArchiveDecision.Error, Reason = exception.Message });
            }
            log?.Invoke($"Planned {plan.Items.Count} images: {relative}");
        }
        return plan;
    }
}
