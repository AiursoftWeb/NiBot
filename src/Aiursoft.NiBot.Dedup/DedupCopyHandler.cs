using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NiBot.Dedup.Models;
using Aiursoft.NiBot.Dedup.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Dedup;

public class DedupCopyHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "dedup-copy";
    protected override string Description => "De-duplicate images in a folder and copy them to another folder.";

    private static readonly Option<string> SourcePathOptions = new(
        ["--source", "-s"],
        "Path of the folder to de-duplicate. (Symbolic links will be ignored)")
    {
        IsRequired = true
    };
    
    private static readonly Option<string> DestinationPathOptions = new(
        ["--destination", "-d"],
        "Path of the folder to copy de-duplicated images.")
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
        "Recursively search for similar images in subdirectories. (.trash content will be ignored)");
    
    private static readonly Option<KeepPreference[]> KeepOption = new(
        ["--keep", "-k"],
        () => [KeepPreference.Colorful, KeepPreference.HighestResolution, KeepPreference.Largest, KeepPreference.Newest],
        "Preference for sorting images by quality to determine which to keep when duplicates are found. Available options: Colorful|GrayScale|Newest|Oldest|Smallest|Largest|HighestResolution|LowestResolution.");

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
            SourcePathOptions,
            DestinationPathOptions,
            SimilarityBar,
            RecursiveOption,
            KeepOption,
            YesOption,
            ExtensionsOption,
            ThreadsOption
        };
    }
    
    protected override async Task Execute(InvocationContext context)
    {
        var verbose = context.ParseResult.GetValueForOption(CommonOptionsProvider.VerboseOption);
        var sourcePath = context.ParseResult.GetValueForOption(SourcePathOptions)!;
        var destinationPath = context.ParseResult.GetValueForOption(DestinationPathOptions)!;
        var similarityBar = context.ParseResult.GetValueForOption(SimilarityBar);
        var recursive = context.ParseResult.GetValueForOption(RecursiveOption);
        var keep = context.ParseResult.GetValueForOption(KeepOption);
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

        var absoluteSourcePath = Path.IsPathRooted(sourcePath)
            ? Path.GetFullPath(sourcePath)
            : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, sourcePath));
        var absoluteDestinationPath = Path.IsPathRooted(destinationPath)
            ? Path.GetFullPath(destinationPath)
            : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, destinationPath));
        await dedupEngine.DedupCopyAsync(
            sourceFolder: absoluteSourcePath,
            destinationFolder: absoluteDestinationPath,
            similarityBar: similarityBar,
            recursive: recursive,
            keepPreferences: keep,
            interactive: !yes,
            extensions: extensions,
            verbose: verbose,
            threads: threads);
    }
}