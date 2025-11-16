using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NiBot.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Dedup;

public class DedupCopyHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "dedup-copy";
    protected override string Description => "Copy images in a folder to another folder with de-duplication. (Duplicate images in destination folder will be ignored.)";

    protected override IEnumerable<Option> GetCommandOptions()
    {
        return
        [
            Options.SourcePathOptions,
            Options.DestinationPathOptions,
            Options.SimilarityBar,
            Options.RecursiveOption,
            Options.KeepOption,
            Options.YesOption,
            Options.ExtensionsOption,
            Options.ThreadsOption
        ];
    }

    protected override async Task Execute(ParseResult context)
    {
        var verbose = context.GetValue(CommonOptionsProvider.VerboseOption);
        var sourcePath = context.GetValue(Options.SourcePathOptions)!;
        var destinationPath = context.GetValue(Options.DestinationPathOptions)!;
        var similarityBar = context.GetValue(Options.SimilarityBar);
        var recursive = context.GetValue(Options.RecursiveOption);
        var keep = context.GetValue(Options.KeepOption);
        var yes = context.GetValue(Options.YesOption);
        var extensions = context.GetValue(Options.ExtensionsOption);
        var threads = context.GetValue(Options.ThreadsOption);

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
