using System.CommandLine;
using Aiursoft.NiBot.Core.Models;

namespace Aiursoft.NiBot.Dedup;

public static class Options
{
    public static readonly Option<string> SourcePathOptions = new(
        name: "--source",
        aliases: ["-s"])
    {
        Description = "Path of the folder to de-duplicate. (Symbolic links will be ignored)",
        Required = true
    };

    public static readonly Option<string> DestinationPathOptions = new(
        name: "--destination",
        aliases: ["-d"])
    {
        Description = "Path of the folder to copy de-duplicated images.",
        Required = true
    };

    public static readonly Option<string> PathOptions = new(
        name: "--path",
        aliases: ["-p"])
    {
        Description = "Path of the folder to de-duplicate. (Symbolic links will be ignored)",
        Required = true
    };

    public static readonly Option<int> SimilarityBar = new(
        name: "--duplicate-similar",
        aliases: ["-ds"])
    {
        DefaultValueFactory = _ => 96,
        Description = "Similarity bar. This value means two image are considered as duplicates if their similarity is greater than it. Setting too small may cause different images to be considered as duplicates. Suggested values: [96-100]"
    };

    public static readonly Option<bool> RecursiveOption = new(
        name: "--recursive",
        aliases: ["-r"])
    {
        DefaultValueFactory = _ => false,
        Description = "Recursively search for similar images in subdirectories. (.trash content will be ignored)"
    };

    public static readonly Option<KeepPreference[]> KeepOption = new(
        name: "--keep",
        aliases: ["-k"])
    {
        DefaultValueFactory = _ =>
        [
            KeepPreference.Colorful, KeepPreference.HighestResolution, KeepPreference.Largest, KeepPreference.Newest
        ],
        Description = "Preference for sorting images by quality to determine which to keep when duplicates are found. Available options: Colorful|GrayScale|Newest|Oldest|Smallest|Largest|HighestResolution|LowestResolution."
    };

    public static readonly Option<DuplicateAction> ActionOption = new(
        name: "--action",
        aliases: ["-a"])
    {
        DefaultValueFactory = _ => DuplicateAction.MoveToTrash,
        Description = "Action to take when duplicates are found. Available options: Nothing, Delete, MoveToTrash."
    };

    public static readonly Option<bool> YesOption = new(
        name: "--yes",
        aliases: ["-y"])
    {
        DefaultValueFactory = _ => false,
        Description = "No interactive mode. Taking action without asking for confirmation."
    };

    public static readonly Option<string[]> ExtensionsOption = new(
        name: "--extensions",
        aliases: ["-e"])
    {
        DefaultValueFactory = _ => ["jpg", "jpeg", "png", "jfif"],
        Description = "Extensions of files to dedup."
    };

    public static readonly Option<int> ThreadsOption = new(
        name: "--threads",
        aliases: ["-t"])
    {
        DefaultValueFactory = _ => Environment.ProcessorCount,
        Description = $"Number of threads to use for image indexing. Default is {Environment.ProcessorCount}."
    };
}
