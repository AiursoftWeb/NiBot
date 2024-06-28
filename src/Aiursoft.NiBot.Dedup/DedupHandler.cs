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
        
        var calendar = services.GetRequiredService<DedupEngine>();
        await calendar.DedupAsync(
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

public class CompareHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "compare";
    
    protected override string Description => "Compare two images to see their similarity.";
    
    private static readonly Option<string[]> PathsOptions = new(
        ["--images", "-i"],
        "Paths of the images to compare.")
    {
        IsRequired = true
    };
    
    protected override IEnumerable<Option> GetCommandOptions()
    {
        return new Option[]
        {
            PathsOptions
        };
    }
    
    protected override async Task Execute(InvocationContext context)
    {
        var verbose = context.ParseResult.GetValueForOption(CommonOptionsProvider.VerboseOption);
        var paths = context.ParseResult.GetValueForOption(PathsOptions)!;

        if (paths.Length < 2)
        {
            throw new ArgumentException("At least two images should be provided for comparison.");
        }
        var services = ServiceBuilder
            .CreateCommandHostBuilder<Startup>(verbose)
            .Build()
            .Services;
        var imageHasher = services.GetRequiredService<ImageHasher>();

        var mappedImages = await imageHasher.MapImagesAsync(paths, showProgress: !verbose, Environment.ProcessorCount);

        foreach (var mappedImage in mappedImages)
        {
            Console.WriteLine($"Image: {mappedImage.PhysicalPath}");
            Console.WriteLine($"\t\tHash: \t{mappedImage.Hash}");
            Console.WriteLine($"\t\tSize: \t{mappedImage.Size}");
            Console.WriteLine($"\t\tResolution: \t{mappedImage.Resolution}");
            Console.WriteLine($"\t\tIsGrayscale: \t{mappedImage.IsGrayscale}");
        }
        
        for (int i = 0; i < paths.Length; i++)
        {
            for (int j = i + 1; j < paths.Length; j++)
            {
                var hash1 = mappedImages[i];
                var hash2 = mappedImages[j];
                var ratio = hash1.ImageDiffRatio(hash2);
                Console.WriteLine($"Similarity between {paths[i]} and {paths[j]}: {ratio:P}");
            }
        }
    }
}