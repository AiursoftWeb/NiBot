using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.NiBot.Archive;

namespace Aiursoft.NiBot.Dedup;

public sealed class ArchiveHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "archive";
    protected override string Description => "Plan incremental image imports: skip duplicates and recommend existing category folders. Writes a JSON/HTML preview by default.";

    private static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NiBot");
    private static readonly Option<string> Model = new("--model")
    {
        Description = "Compatible local CLIP vision ONNX model; see archive-model and docs/archive.md.",
        DefaultValueFactory = _ => Path.Combine(DataDirectory, "models", "clip-vit-large-patch14.onnx")
    };
    private static readonly Option<string> Cache = new("--cache")
    {
        Description = "Content-addressed feature cache, outside source and destination.",
        DefaultValueFactory = _ => Path.Combine(DataDirectory, "archive-cache")
    };
    private static readonly Option<string> Report = new("--report")
    {
        Description = "New JSON report path; defaults to NiBot/reports in local application data. An HTML preview is written alongside it.",
        DefaultValueFactory = _ => Path.Combine(DataDirectory, "reports", $"archive-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json")
    };
    private static readonly Option<ArchiveClassificationMode> Mode = new("--classification") { DefaultValueFactory = _ => ArchiveClassificationMode.Strict, Description = "Strict requires category thresholds; Nearest chooses the first-ranked category. Duplicate checks remain enabled." };
    private static readonly Option<bool> Apply = new("--apply") { Description = "Explicitly copy Ready items after generating the report. Never overwrite old files." };
    private static readonly Option<bool> SkipSimilar = new("--skip-similar") { Description = "Also skip perceptual duplicate candidates automatically. Default: leave them for review." };
    private static readonly Option<double> Duplicate = new("--duplicate-similar") { DefaultValueFactory = _ => 98, Description = "pHash duplicate-candidate threshold, 0–100." };
    private static readonly Option<double> Similarity = new("--min-similarity") { DefaultValueFactory = _ => 0.75, Description = "Minimum category centroid cosine similarity, 0–1." };
    private static readonly Option<double> Margin = new("--min-margin") { DefaultValueFactory = _ => 0.03, Description = "Minimum cosine margin over the second category, 0–2." };
    private static readonly Option<double> Support = new("--min-support") { DefaultValueFactory = _ => 0.8, Description = "Minimum fraction of nearest neighbors supporting the winning category, 0–1." };
    private static readonly Option<int> Neighbors = new("--neighbors") { DefaultValueFactory = _ => 5, Description = "Number of nearest reference images used for voting." };
    private static readonly Option<int> Threads = new("--inference-threads") { DefaultValueFactory = _ => 2, Description = "CPU threads for ONNX inference." };
    private static readonly Option<string[]> Exclude = new("--exclude-category")
    {
        DefaultValueFactory = _ => ["Added", "_review"],
        Description = "Top-level folders used for deduplication but not classification. Repeat for multiple values; replaces defaults."
    };

    protected override IEnumerable<Option> GetCommandOptions() =>
    [Options.SourcePathOptions, Options.DestinationPathOptions, Model, Cache, Report, Apply, SkipSimilar, Mode,
        Duplicate, Similarity, Margin, Support, Neighbors, Threads, Exclude];

    protected override Task Execute(ParseResult context)
    {
        var source = ArchiveFiles.Root(context.GetValue(Options.SourcePathOptions)!);
        var destination = ArchiveFiles.Root(context.GetValue(Options.DestinationPathOptions)!);
        ArchiveFiles.AssertDisjoint(source, destination);
        var settings = new ArchiveSettings
        {
            ClassificationMode = context.GetValue(Mode),
            DuplicateSimilarity = context.GetValue(Duplicate), SkipSimilar = context.GetValue(SkipSimilar),
            MinimumSimilarity = context.GetValue(Similarity), MinimumMargin = context.GetValue(Margin),
            MinimumSupport = context.GetValue(Support), Neighbors = context.GetValue(Neighbors),
            ExcludedCategories = context.GetValue(Exclude) ?? []
        };
        settings.Validate();
        var report = Path.GetFullPath(context.GetValue(Report)!);
        var placeholder = new ArchivePlan { SourceRoot = source, DestinationRoot = destination, ModelFingerprint = "", Settings = settings };
        ArchiveReport.ValidateOutput(report, placeholder);
        if (File.Exists(report) || File.Exists(report + ".html")) throw new IOException("Report exists; choose a new path.");
        using var cancellation = new ArchiveCancellation();
        using var provider = new OnnxClipEmbeddingProvider(context.GetValue(Model)!, context.GetValue(Threads));
        var planner = new ArchivePlanner(provider, context.GetValue(Cache)!, Console.WriteLine);
        var plan = planner.Create(source, destination, settings, cancellation.Token);
        ArchiveReport.Write(report, plan);
        Console.WriteLine($"Report: {report}\nPreview: {report}.html");
        foreach (var group in plan.Items.GroupBy(i => i.Decision)) Console.WriteLine($"{group.Key}: {group.Count()}");
        if (context.GetValue(Apply)) ArchiveApplyHandler.ApplyPlan(plan, report, cancellation.Token);
        if (plan.Items.Any(i => i.Decision == ArchiveDecision.Error))
            throw new IOException("Some images could not be planned; details are in the report.");
        return Task.CompletedTask;
    }
}
