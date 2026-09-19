using System.Text.Json.Serialization;

namespace Aiursoft.NiBot.Archive;

[JsonConverter(typeof(JsonStringEnumConverter<ArchiveDecision>))]
public enum ArchiveDecision
{
    Duplicate,
    ReviewDuplicate,
    Ready,
    Review,
    Error
}

[JsonConverter(typeof(JsonStringEnumConverter<ArchiveClassificationMode>))]
public enum ArchiveClassificationMode { Strict, Nearest }

public sealed record ArchiveSettings
{
    public ArchiveClassificationMode ClassificationMode { get; init; } = ArchiveClassificationMode.Strict;
    public double DuplicateSimilarity { get; init; } = 98;
    public bool SkipSimilar { get; init; }
    public double MinimumSimilarity { get; init; } = 0.75;
    public double MinimumMargin { get; init; } = 0.03;
    public double MinimumSupport { get; init; } = 0.8;
    public int Neighbors { get; init; } = 5;
    public string[] ExcludedCategories { get; init; } = ["Added", "_review"];

    public void Validate()
    {
        if (!Enum.IsDefined(ClassificationMode) || !double.IsFinite(DuplicateSimilarity) || DuplicateSimilarity is < 0 or > 100 ||
            !double.IsFinite(MinimumSimilarity) || MinimumSimilarity is < 0 or > 1 ||
            !double.IsFinite(MinimumMargin) || MinimumMargin is < 0 or > 2 ||
            !double.IsFinite(MinimumSupport) || MinimumSupport is < 0 or > 1 || Neighbors < 1)
        {
            throw new ArgumentException("Invalid archive thresholds or neighbor count.");
        }
    }
}

public sealed record ArchiveCandidate(string Folder, double Similarity, double Support, string[] Examples);

public sealed record ArchiveItem
{
    public required string Source { get; init; }
    public string Sha256 { get; init; } = "";
    public required ArchiveDecision Decision { get; init; }
    public required string Reason { get; init; }
    public string? DuplicateOf { get; init; }
    public string? TargetFolder { get; init; }
    public ArchiveCandidate[] Candidates { get; init; } = [];
}

public sealed record ArchivePlan
{
    public int Version { get; init; } = 1;
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string SourceRoot { get; init; }
    public required string DestinationRoot { get; init; }
    public required string ModelFingerprint { get; init; }
    public required ArchiveSettings Settings { get; init; }
    public string[] Categories { get; init; } = [];
    public Dictionary<string, string> DestinationHashes { get; init; } = new(StringComparer.Ordinal);
    public List<ArchiveItem> Items { get; init; } = [];
}

public sealed record IndexedPhoto(string Path, string RelativePath, string Sha256, ulong PerceptualHash, string? Category);
public sealed record ReferencePhoto(IndexedPhoto Photo, float[] Vector);
public sealed record ApplyResult(string Source, string Status, string? Destination, string? Error);

public interface IImageEmbeddingProvider : IDisposable
{
    string Fingerprint { get; }
    float[] Embed(string path);
}
