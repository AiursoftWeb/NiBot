using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NiBot.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Dedup;

public class DedupPatchHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "dedup-patch";
    
    protected override string Description => "Dedup patch will fetch all files from source directory and destination directory, and if a picture in source directory is a duplicate of a picture in destination directory, and source picture has a higher quality, then the source picture will be patched to the destination picture.";
    
    protected override IEnumerable<Option> GetCommandOptions()
    {
        return new Option[]
        {
            Options.SourcePathOptions,
            Options.DestinationPathOptions,
            Options.SimilarityBar,
            Options.KeepOption,
            Options.ExtensionsOption,
            Options.ThreadsOption
        };
    }
    
    protected override async Task Execute(InvocationContext context)
    {
        var verbose = context.ParseResult.GetValueForOption(CommonOptionsProvider.VerboseOption);
        var sourcePath = context.ParseResult.GetValueForOption(Options.SourcePathOptions)!;
        var destinationPath = context.ParseResult.GetValueForOption(Options.DestinationPathOptions)!;
        var similarityBar = context.ParseResult.GetValueForOption(Options.SimilarityBar);
        var keep = context.ParseResult.GetValueForOption(Options.KeepOption);
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
        await dedupEngine.DedupPatchAsync(
            sourceFolder: absoluteSourcePath,
            destinationFolder: absoluteDestinationPath,
            similarityBar: similarityBar,
            keepPreferences: keep,
            extensions: extensions,
            verbose: verbose,
            threads: threads);
    }
}