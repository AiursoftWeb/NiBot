using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.NiBot.Archive;

namespace Aiursoft.NiBot.Dedup;

public sealed class ArchiveApplyHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "archive-apply";
    protected override string Description => "Copy Ready items from a reviewed archive JSON plan. Revalidates source hashes, preserves existing files and skips already imported content.";
    private static readonly Option<string> Plan = new("--plan") { Required = true, Description = "JSON plan produced by nibot archive." };
    protected override IEnumerable<Option> GetCommandOptions() => [Plan];

    protected override Task Execute(ParseResult context)
    {
        var path = Path.GetFullPath(context.GetValue(Plan)!);
        var plan = ArchiveReport.Read(path);
        using var cancellation = new ArchiveCancellation();
        ApplyPlan(plan, path, cancellation.Token);
        return Task.CompletedTask;
    }

    public static void ApplyPlan(ArchivePlan plan, string report, CancellationToken cancellationToken)
    {
        var output = report + ".results.json";
        ArchiveReport.ValidateOutput(output, plan);
        var results = new ArchiveExecutor().Apply(plan, cancellationToken);
        ArchiveFiles.WriteJson(output, results);
        foreach (var group in results.GroupBy(i => i.Status)) Console.WriteLine($"{group.Key}: {group.Count()}");
        foreach (var result in results.Where(i => i.Status == "Error")) Console.Error.WriteLine($"{result.Source}: {result.Error}");
        Console.WriteLine($"Apply results: {output}");
        if (results.Any(i => i.Status == "Error"))
            throw new IOException("Some files were not copied; inspect the results report. The plan can be safely retried.");
    }
}
