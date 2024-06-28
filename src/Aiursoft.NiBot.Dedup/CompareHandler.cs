using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NiBot.Dedup.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Dedup;

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
                var ratio = hash1.ImageSimilarityRatio(hash2);
                Console.WriteLine($"Similarity between {paths[i]} and {paths[j]}: {ratio:P}");
            }
        }
    }
}