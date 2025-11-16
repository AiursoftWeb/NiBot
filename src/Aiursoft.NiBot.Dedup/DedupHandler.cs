using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NiBot.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Dedup;

public class DedupHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "dedup";
    protected override string Description => "De-duplicate images in a folder.";

    protected override IEnumerable<Option> GetCommandOptions()
    {
        return
        [
            Options.PathOptions,
            Options.SimilarityBar,
            Options.RecursiveOption,
            Options.KeepOption,
            Options.ActionOption,
            Options.YesOption,
            Options.ExtensionsOption,
            Options.ThreadsOption
        ];
    }

    protected override async Task Execute(ParseResult context)
    {
        var verbose = context.GetValue(CommonOptionsProvider.VerboseOption);
        var path = context.GetValue(Options.PathOptions)!;
        var similarityBar = context.GetValue(Options.SimilarityBar);
        var recursive = context.GetValue(Options.RecursiveOption);
        var keep = context.GetValue(Options.KeepOption);
        var action = context.GetValue(Options.ActionOption);
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

        var absolutePath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
        await dedupEngine.DedupAsync(
            path: absolutePath,
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