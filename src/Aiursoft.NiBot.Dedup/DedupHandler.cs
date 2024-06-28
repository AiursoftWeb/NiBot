using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NiBot.Dedup.Models;
using Aiursoft.NiBot.Dedup.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Dedup;

public class DedupHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "dedup";
    protected override string Description => "De-duplicate images in a folder.";

    private static readonly Option<string> PathOptions = new(
        ["--path", "-p"],
        "Path of the folder to dedup.")
    {
        IsRequired = true
    };
    
    private static readonly Option<int> SimilarityBar = new(
        ["--duplicate-similar", "-ds"],
        () => 96,
        "Similarity bar. This value means two image are considered as duplicates if their similarity is greater than it. Setting too small may cause different images to be considered as duplicates. Suggested values: [96-100]");
     
    private static readonly Option<bool> RecursiveOption = new(
        ["--recursive", "-r"],
        () => false,
        "Recursively search for similar images in subdirectories.");

    private static readonly Option<KeepPreference[]> KeepOption = new(
        ["--keep", "-k"],
        () => [KeepPreference.Colorful, KeepPreference.HighestResolution, KeepPreference.Largest, KeepPreference.Newest],
        "Preference for sorting images by quality to determine which to keep when duplicates are found. Available options: Colorful|GrayScale|Newest|Oldest|Smallest|Largest|HighestResolution|LowestResolution.");

    private static readonly Option<DuplicateAction> ActionOption = new(
        ["--action", "-a"],
        () => DuplicateAction.MoveToTrash,
        "Action to take when duplicates are found. Available options: Nothing, Delete, MoveToTrash.");
    
    private static readonly Option<bool> YesOption = new(
        ["--yes", "-y"],
        () => false,
        "No interactive mode. Taking action without asking for confirmation.");

    private static readonly Option<string[]> ExtensionsOption = new(
        ["--extensions", "-e"],
        () => ["jpg", "jpeg", "png", "jfif"],
        "Extensions of files to dedup.");
    
    private static readonly Option<int> ThreadsOption = new(
        ["--threads", "-t"],
        () => Environment.ProcessorCount,
        $"Number of threads to use for image indexing. Default is {Environment.ProcessorCount}.");
   
    protected override IEnumerable<Option> GetCommandOptions()
    {
        return new Option[]
        {
            PathOptions,
            SimilarityBar,
            RecursiveOption,
            KeepOption,
            ActionOption,
            YesOption,
            ExtensionsOption,
            ThreadsOption
        };
    }

    protected override async Task Execute(InvocationContext context)
    {
        var verbose = context.ParseResult.GetValueForOption(CommonOptionsProvider.VerboseOption);
        var path = context.ParseResult.GetValueForOption(PathOptions)!;
        var similarityBar = context.ParseResult.GetValueForOption(SimilarityBar);
        var recursive = context.ParseResult.GetValueForOption(RecursiveOption);
        var keep = context.ParseResult.GetValueForOption(KeepOption);
        var action = context.ParseResult.GetValueForOption(ActionOption);
        var yes = context.ParseResult.GetValueForOption(YesOption);
        var extensions = context.ParseResult.GetValueForOption(ExtensionsOption);
        var threads = context.ParseResult.GetValueForOption(ThreadsOption);
        
        if (!(keep?.Any() ?? false)) throw new ArgumentException("At least one preference should be provided for --keep.");
        if (!(extensions?.Any() ?? false)) throw new ArgumentException("At least one extension should be provided for --extensions.");
        
        var services = ServiceBuilder
            .CreateCommandHostBuilder<Startup>(verbose)
            .Build()
            .Services;
        
        var dedupEngine = services.GetRequiredService<DedupEngine>();
        await dedupEngine.DedupAsync(
            path: path,
            similarityBar: similarityBar,
            recursive: recursive,
            keepPreferences: keep,
            action: action,
            interactive: !yes,
            extensions: extensions,
            verbose: verbose,
            threads: threads);
    }
}