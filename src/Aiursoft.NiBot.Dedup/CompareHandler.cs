using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NiBot.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Dedup;

public class CompareHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "compare";

    protected override string Description => "Compare two images to see their similarity.";

    private static readonly Option<string[]> PathsOptions = new(
        ["--images", "-i"],
        "Paths of the images to compare. You can pass this '-i' parameter multiple times to compare at least two images.")
    {
        IsRequired = true
    };

    protected override IEnumerable<Option> GetCommandOptions()
    {
        return
        [
            PathsOptions
        ];
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

        var physicalPaths = paths
            .Select(Path.GetFullPath)
            .Where(File.Exists)
            .ToArray();
        var mappedImages =
            await imageHasher.MapImagesAsync(physicalPaths, showProgress: !verbose, Environment.ProcessorCount);

        if (verbose)
        {
            foreach (var mappedImage in mappedImages)
            {
                Console.WriteLine($"Image: {mappedImage.PhysicalPath}");

                // Display hash as hex.
                Console.WriteLine($"\tHash: \t\t{mappedImage.Hash:X}");
                Console.WriteLine($"\tSize: \t\t{mappedImage.Size}");
                Console.WriteLine($"\tResolution: \t{mappedImage.Resolution}");
                Console.WriteLine($"\tIsGrayscale: \t{mappedImage.IsGrayscale}");
            }
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