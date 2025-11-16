using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NiBot.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Dedup
{
    public class DupTopHandler : ExecutableCommandHandlerBuilder
    {
        protected override string Name => "dup-top";
        protected override string Description => "Find top similar images in a folder compared to a source image.";

        private static readonly Option<string> SourceOption = new(
                ["--input", "-i"],
            "Path to the source image for comparison.")
        { IsRequired = true };

        private static readonly Option<int> TopOption = new(
            ["--top"],
            () => 15,
            "Number of top similar images to return.");

        protected override IEnumerable<Option> GetCommandOptions()
        {
            return [
                SourceOption,
                Options.SourcePathOptions,
                Options.RecursiveOption,
                Options.ExtensionsOption,
                Options.ThreadsOption,
                TopOption
            ];
        }

        protected override async Task Execute(ParseResult context)
        {
            var verbose = context.GetValue(CommonOptionsProvider.VerboseOption);
            var sourcePath = context.GetValue(SourceOption)!;
            var folderPath = context.GetValue(Options.SourcePathOptions)!;
            var recursive = context.GetValue(Options.RecursiveOption);
            var top = context.GetValue(TopOption);
            var extensions = context.GetValue(Options.ExtensionsOption)!;
            var threads = context.GetValue(Options.ThreadsOption);

            var services = ServiceBuilder
                .CreateCommandHostBuilder<Startup>(verbose)
                .Build()
                .Services;
            var imageHasher = services.GetRequiredService<ImageHasher>();
            var filesHelper = services.GetRequiredService<FilesHelper>();

            var absoluteSource = Path.GetFullPath(sourcePath);
            var absoluteFolder = Path.GetFullPath(folderPath);

            var sourceMapped = await imageHasher.MapImagesAsync([absoluteSource], showProgress: !verbose, threads: 1);
            var sourceImage = sourceMapped.First();

            var mappedImages = await filesHelper.GetPhotosUnderPath(
                path: absoluteFolder,
                recursive: recursive,
                verbose: verbose,
                extensions: extensions,
                includeSymbolicLinks: false,
                threads: threads);

            var results = mappedImages
                .Select(m => new { Path = m.PhysicalPath, Ratio = m.ImageSimilarityRatio(sourceImage) })
                .OrderByDescending(x => x.Ratio)
                .Take(top)
                .ToList();

            Console.WriteLine($"Top {top} similar images for '{absoluteSource}':");
            foreach (var item in results)
            {
                Console.WriteLine($"{item.Path}: {item.Ratio:P2}");
            }
        }
    }
}
