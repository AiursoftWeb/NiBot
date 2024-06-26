using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Microsoft.Extensions.DependencyInjection;
using NiBot.Dedup.Models;
using NiBot.Dedup.Services;

namespace NiBot.Dedup;

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
        () => 99,
        "Similarity bar. Default is 99. This value means two image are considered as duplicates if their similarity is greater than 99%. Setting too small may cause different images to be considered as duplicates.");
     
    private static readonly Option<bool> RecursiveOption = new(
        ["--recursive", "-r"],
        () => false,
        "Recursively search for similar images in subdirectories. Default is false.");

    private static readonly Option<KeepPreference[]> KeepOption = new(
        ["--keep", "-k"],
        () => [KeepPreference.Colorful, KeepPreference.HighestResolution, KeepPreference.Largest, KeepPreference.Newest],
        "Preference for sorting images by quality to determine which to keep when duplicates are found. Default is [HighestResolution,Largest,Newest]. Available options: Newest, Oldest, Smallest, Largest, HighestResolution, LowestResolution.");

    private static readonly Option<DuplicateAction> ActionOption = new(
        ["--action", "-a"],
        () => DuplicateAction.Nothing,
        "Action to take when duplicates are found. Default is Delete. Available options: Nothing, Delete, MoveToTrash, MoveAndCopyOriginalToTrash.");
    
    private static readonly Option<bool> InteractiveOption = new(
        ["--interactive", "-i"],
        () => false,
        "Interactive mode. Ask for confirmation before deleting files. Default is false.");

    private static readonly Option<string[]> ExtensionsOption = new(
        ["--extensions", "-e"],
        () => ["jpg", "jpeg", "png", "jfif"],
        "Extensions of files to dedup. Default is jpg, jpeg, png, jfif.");
   
    protected override IEnumerable<Option> GetCommandOptions()
    {
        return new Option[]
        {
            PathOptions,
            SimilarityBar,
            RecursiveOption,
            KeepOption,
            ActionOption,
            InteractiveOption,
            ExtensionsOption
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
        var interactive = context.ParseResult.GetValueForOption(InteractiveOption);
        var extensions = context.ParseResult.GetValueForOption(ExtensionsOption);
        
        if (!keep?.Any() ?? true) throw new ArgumentException("At least one preference should be provided for --keep.");
        if (!extensions?.Any() ?? true) throw new ArgumentException("At least one extension should be provided for --extensions.");
        
        var services = ServiceBuilder
            .CreateCommandHostBuilder<Startup>(verbose)
            .Build()
            .Services;
        
        var calendar = services.GetRequiredService<DedupEngine>();
        await calendar.DedupAsync(path, similarityBar, recursive, keep, action, interactive, extensions, verbose);
    }
}