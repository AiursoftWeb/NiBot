using System.Numerics;
using System.Text.RegularExpressions;

namespace Aiursoft.NiBot.Archive;

public sealed partial class ArchiveExecutor
{
    public List<ApplyResult> Apply(ArchivePlan plan, CancellationToken cancellationToken = default)
    {
        if (plan.Version != 1) throw new InvalidDataException("Unsupported archive plan version.");
        var source = ArchiveFiles.Root(plan.SourceRoot);
        var destination = ArchiveFiles.Root(plan.DestinationRoot);
        ArchiveFiles.AssertDisjoint(source, destination);
        plan.Settings.Validate();
        var ready = plan.Items.Where(i => i.Decision == ArchiveDecision.Ready).ToArray();
        // Validate the entire write set before touching the destination.
        foreach (var item in ready)
        {
            ArchiveFiles.Resolve(source, item.Source);
            if (!ArchiveFiles.IsImage(item.Source) || !Sha256Pattern().IsMatch(item.Sha256))
                throw new InvalidDataException("Plan contains a non-image or invalid content hash.");
            if (item.TargetFolder == null || !plan.Categories.Contains(item.TargetFolder, StringComparer.Ordinal) ||
                item.TargetFolder != Path.GetFileName(item.TargetFolder) || item.TargetFolder.StartsWith('.') ||
                plan.Settings.ExcludedCategories.Contains(item.TargetFolder, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Target must be an existing reference category in the plan.");
            if (!Directory.Exists(ArchiveFiles.Resolve(destination, item.TargetFolder)))
                throw new DirectoryNotFoundException($"Category no longer exists: {item.TargetFolder}");
        }

        if (ready.Length == 0) return [];
        var stateRoot = Path.Combine(destination, ".nibot-archive");
        ArchiveFiles.AssertNoLinks(stateRoot);
        Directory.CreateDirectory(stateRoot);
        var lockPath = Path.Combine(stateRoot, "apply.lock");
        ArchiveFiles.AssertNoLinks(lockPath);
        using var archiveLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var existing = new Dictionary<string, string>(StringComparer.Ordinal);
        var arrivals = new List<IndexedPhoto>();
        foreach (var path in ArchiveFiles.Images(destination))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hash = ArchiveFiles.Hash(path);
            existing.TryAdd(hash, path);
            if (!plan.DestinationHashes.TryGetValue(Path.GetRelativePath(destination, path), out var previous) || previous != hash)
                arrivals.Add(ArchiveFiles.Index(destination, path, plan.Settings));
        }

        var results = new List<ApplyResult>();
        foreach (var item in ready)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? temporary = null;
            try
            {
                var input = ArchiveFiles.Resolve(source, item.Source);
                if (ArchiveFiles.Hash(input) != item.Sha256)
                    throw new IOException("Source changed since planning; create a new plan.");
                if (existing.TryGetValue(item.Sha256, out var found))
                {
                    results.Add(new ApplyResult(item.Source, "AlreadyPresent", found, null));
                    continue;
                }

                var incoming = ArchiveFiles.Index(source, input, plan.Settings);
                var newDuplicate = arrivals.FirstOrDefault(p =>
                    (64 - BitOperations.PopCount(p.PerceptualHash ^ incoming.PerceptualHash)) * 100.0 / 64 >= plan.Settings.DuplicateSimilarity);
                if (newDuplicate != null)
                {
                    results.Add(new ApplyResult(item.Source, plan.Settings.SkipSimilar ? "SkippedSimilar" : "ReviewDuplicate",
                        newDuplicate.Path, "A similar image arrived after planning. Re-plan or inspect before copying."));
                    continue;
                }

                var target = ArchiveFiles.Resolve(destination, Path.Combine(item.TargetFolder!, Path.GetFileName(item.Source)));
                if (File.Exists(target) || Directory.Exists(target))
                {
                    var stem = string.Concat(Path.GetFileNameWithoutExtension(item.Source).EnumerateRunes().Take(32));
                    var alternate = stem + "__" + item.Sha256 + Path.GetExtension(item.Source);
                    target = ArchiveFiles.Resolve(destination, Path.Combine(item.TargetFolder!, alternate));
                }
                if (File.Exists(target) || Directory.Exists(target))
                    throw new IOException("Both destination names are occupied; no file was overwritten.");

                temporary = ArchiveFiles.Resolve(destination, Path.Combine(item.TargetFolder!, ".nibot-" + Guid.NewGuid().ToString("N") + ".tmp"));
                using (var inputStream = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var outputStream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    inputStream.CopyTo(outputStream);
                    outputStream.Flush(true);
                }
                if (ArchiveFiles.Hash(temporary) != item.Sha256)
                    throw new IOException("Source changed during copying; temporary copy was discarded.");
                File.SetLastWriteTimeUtc(temporary, File.GetLastWriteTimeUtc(input));
                var journal = ArchiveFiles.Resolve(destination, Path.Combine(".nibot-archive", "imports", item.Sha256 + ".json"));
                // Record before publication: even a crash immediately after the move cannot make an
                // imported image become a training example in the next run.
                ArchiveFiles.WriteJson(journal, new ImportRecord(item.Sha256, Path.GetRelativePath(destination, target)));
                ArchiveFiles.AssertNoLinks(target);
                File.Move(temporary, target, false);
                temporary = null;
                existing.Add(item.Sha256, target);
                results.Add(new ApplyResult(item.Source, "Copied", target, null));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(new ApplyResult(item.Source, "Error", null, exception.Message));
            }
            finally
            {
                if (temporary != null && File.Exists(temporary)) File.Delete(temporary);
            }
        }
        return results;
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
