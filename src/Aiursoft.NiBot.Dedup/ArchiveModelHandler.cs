using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.NiBot.Archive;

namespace Aiursoft.NiBot.Dedup;

public sealed class ArchiveModelHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "archive-model";
    protected override string Description => "Install a compatible local or HTTPS ONNX model after verifying its SHA-256 and model contract.";
    private static readonly Option<string> Source = new("--source") { Required = true, Description = "Local model path or final HTTPS download URL." };
    private static readonly Option<string> Checksum = new("--sha256") { Required = true, Description = "Expected SHA-256 from the model publisher." };
    private static readonly Option<string> Destination = new("--destination") { DefaultValueFactory = _ => ArchiveModelInstaller.DefaultPath, Description = "Model installation path; existing different content is never overwritten." };
    protected override IEnumerable<Option> GetCommandOptions() => [Source, Checksum, Destination];

    protected override async Task Execute(ParseResult context)
    {
        using var cancellation = new ArchiveCancellation();
        var destination = context.GetValue(Destination)!;
        await ArchiveModelInstaller.InstallAsync(context.GetValue(Source)!, destination, context.GetValue(Checksum)!, cancellation.Token);
        Console.WriteLine($"Model ready: {Path.GetFullPath(destination)}");
    }
}
