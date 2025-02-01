using System.CommandLine;
using System.CommandLine.Invocation;
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
    
    protected override async Task Execute(InvocationContext context)
    {
        var verbose = context.ParseResult.GetValueForOption(CommonOptionsProvider.VerboseOption);
        var sourcePath = context.ParseResult.GetValueForOption(Options.SourcePathOptions)!;
        var destinationPath = context.ParseResult.GetValueForOption(Options.DestinationPathOptions)!;
        var similarityBar = context.ParseResult.GetValueForOption(Options.SimilarityBar);
        var recursive = context.ParseResult.GetValueForOption(Options.RecursiveOption);
        var keep = context.ParseResult.GetValueForOption(Options.KeepOption);
        var yes = context.ParseResult.GetValueForOption(Options.YesOption);
        var extensions = context.ParseResult.GetValueForOption(Options.ExtensionsOption);
        var threads = context.ParseResult.GetValueForOption(Options.ThreadsOption);
        
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