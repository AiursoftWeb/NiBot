using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NiBot.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Dedup;

public class ClusterDistributeHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "cluster-distribute";

    protected override string Description => "Distribute images in a directory into folders based on their similarity.";

    protected override IEnumerable<Option> GetCommandOptions()
    {
        return
        [
            Options.PathOptions,
            Options.SimilarityBar,
            Options.RecursiveOption,
            Options.YesOption,
            Options.ExtensionsOption,
            Options.ThreadsOption
        ];
    }

    protected override async Task Execute(InvocationContext context)
    {
        var verbose = context.ParseResult.GetValueForOption(CommonOptionsProvider.VerboseOption);
        var path = context.ParseResult.GetValueForOption(Options.PathOptions)!;
        var similarityBar = context.ParseResult.GetValueForOption(Options.SimilarityBar);
        var recursive = context.ParseResult.GetValueForOption(Options.RecursiveOption);
        var yes = context.ParseResult.GetValueForOption(Options.YesOption);
        var extensions = context.ParseResult.GetValueForOption(Options.ExtensionsOption);
        var threads = context.ParseResult.GetValueForOption(Options.ThreadsOption);

        if (!(extensions?.Any() ?? false))
            throw new ArgumentException("At least one extension should be provided for --extensions.");

        var services = ServiceBuilder
            .CreateCommandHostBuilder<Startup>(verbose)
            .Build()
            .Services;

        var dedupEngine = services.GetRequiredService<DedupEngine>();

        var absolutePath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
        await dedupEngine.ClusterDistributeAsync(
            path: absolutePath,
            similarityBar: similarityBar,
            recursive: recursive,
            interactive: !yes,
            extensions: extensions,
            verbose: verbose,
            threads: threads);
    }
}