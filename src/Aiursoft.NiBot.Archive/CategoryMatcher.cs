namespace Aiursoft.NiBot.Archive;

/// <summary>Ranks a fixed snapshot of existing categories. Imports never update this snapshot.</summary>
public sealed class CategoryMatcher
{
    private readonly ReferencePhoto[] _references;
    private readonly (string Name, float[] Center)[] _centers;

    public CategoryMatcher(ReferencePhoto[] references)
    {
        _references = references;
        if (references.Length == 0)
        {
            throw new ArgumentException("No categorized reference images could be indexed.");
        }

        var dimension = references[0].Vector.Length;
        if (references.Any(r => r.Vector.Length != dimension || r.Photo.Category == null))
        {
            throw new ArgumentException("Reference vectors must share dimensions and have a category.");
        }

        _centers = references.GroupBy(r => r.Photo.Category!, StringComparer.Ordinal).Select(group =>
        {
            var center = new float[dimension];
            foreach (var reference in group)
            {
                for (var i = 0; i < dimension; i++) center[i] += reference.Vector[i];
            }
            return (group.Key, Normalize(center));
        }).OrderBy(g => g.Key, StringComparer.Ordinal).ToArray();
    }

    public ArchiveCandidate[] Rank(float[] vector, int neighbors)
    {
        var nearest = _references.Select(r => (Reference: r, Similarity: Dot(vector, r.Vector)))
            .OrderByDescending(r => r.Similarity)
            .ThenBy(r => r.Reference.Photo.RelativePath, StringComparer.Ordinal).ToArray();
        var voters = nearest.Take(neighbors).ToArray();
        return _centers.Select(c => new ArchiveCandidate(c.Name, Dot(vector, c.Center),
                voters.Count(v => v.Reference.Photo.Category == c.Name) / (double)voters.Length,
                nearest.Where(r => r.Reference.Photo.Category == c.Name).Take(3)
                    .Select(r => r.Reference.Photo.RelativePath).ToArray()))
            .OrderByDescending(c => c.Similarity).ThenBy(c => c.Folder, StringComparer.Ordinal).Take(3).ToArray();
    }

    public static bool IsConfident(ArchiveCandidate[] candidates, ArchiveSettings settings)
    {
        if (candidates.Length == 0) return false;
        var best = candidates[0];
        return best.Similarity >= settings.MinimumSimilarity && best.Support >= settings.MinimumSupport &&
               (candidates.Length == 1 || best.Similarity - candidates[1].Similarity >= settings.MinimumMargin);
    }

    public static float[] Normalize(float[] vector)
    {
        if (vector.Length == 0 || vector.Any(v => !float.IsFinite(v)))
            throw new InvalidDataException("Embedding is empty or contains non-finite values.");
        var norm = Math.Sqrt(vector.Sum(v => (double)v * v));
        if (norm < 1e-12) throw new InvalidDataException("Embedding has zero length.");
        return vector.Select(v => (float)(v / norm)).ToArray();
    }

    private static double Dot(float[] left, float[] right)
    {
        if (left.Length != right.Length) throw new InvalidDataException("Embedding dimensions do not match.");
        double sum = 0;
        for (var i = 0; i < left.Length; i++) sum += (double)left[i] * right[i];
        return Math.Clamp(sum, -1, 1);
    }
}
