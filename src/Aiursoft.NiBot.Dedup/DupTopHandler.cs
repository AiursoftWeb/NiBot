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
        protected override string Name => "duptop";
        protected override string Description => "Find top similar images in a folder compared to a source image.";

        private static readonly Option<string> SourceOption = new(
                ["--source", "-s"],
            "Path to the source image for comparison.")
        { IsRequired = true };

        private static readonly Option<string> FolderOption = new(
                ["--folder", "-f"],
            "Path to the folder containing images to compare against.")
        { IsRequired = true };

        private static readonly Option<int> TopOption = new(
            ["--top", "-t"],
            () => 15,
            "Number of top similar images to return.");

        protected override IEnumerable<Option> GetCommandOptions()
        {
            return [SourceOption, FolderOption, TopOption];
        }

        protected override async Task Execute(InvocationContext context)
        {
            var verbose = context.ParseResult.GetValueForOption(CommonOptionsProvider.VerboseOption);
            var sourcePath = context.ParseResult.GetValueForOption(SourceOption)!;
            var folderPath = context.ParseResult.GetValueForOption(FolderOption)!;
            var top = context.ParseResult.GetValueForOption(TopOption);

            var services = ServiceBuilder
                .CreateCommandHostBuilder<Startup>(verbose)
                .Build()
                .Services;
            var imageHasher = services.GetRequiredService<ImageHasher>();

            // 归一化路径
            var absoluteSource = Path.GetFullPath(sourcePath);
            var absoluteFolder = Path.GetFullPath(folderPath);

            // 加载并计算源图哈希
            var sourceMapped = await imageHasher.MapImagesAsync([absoluteSource], showProgress: !verbose, threads: 1);
            var sourceImage = sourceMapped.First();

            // 获取目标文件夹所有图片
            var files = Directory.GetFiles(absoluteFolder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => !new FileInfo(f).DirectoryName?.EndsWith(".trash") ?? false)
                .Where(f => !services.GetRequiredService<FilesHelper>().IsSymbolicLink(f))
                .ToArray();

            var mappedImages = await imageHasher.MapImagesAsync(files, showProgress: !verbose, threads: Environment.ProcessorCount);

            // 计算相似度并排序
            var results = mappedImages
                .Select(m => new { Path = m.PhysicalPath, Ratio = m.ImageSimilarityRatio(sourceImage) })
                .OrderByDescending(x => x.Ratio)
                .Take(top)
                .ToList();

            // 输出结果
            Console.WriteLine($"Top {top} similar images for '{absoluteSource}':");
            foreach (var item in results)
            {
                Console.WriteLine($"{item.Path}: {item.Ratio:P2}");
            }
        }
    }
}
