using System.CommandLine;
using Aiursoft.NiBot.Core.Models;

namespace Aiursoft.NiBot.Dedup;

public static class Options
{
    public static readonly Option<string> SourcePathOptions = new(
        ["--source", "-s"],
        "Path of the folder to de-duplicate. (Symbolic links will be ignored)")
    {
        IsRequired = true
    };

    public static readonly Option<string> DestinationPathOptions = new(
        ["--destination", "-d"],
        "Path of the folder to copy de-duplicated images.")
    {
        IsRequired = true
    };

    public static readonly Option<string> PathOptions = new(
        ["--path", "-p"],
        "Path of the folder to de-duplicate. (Symbolic links will be ignored)")
    {
        IsRequired = true
    };

    public static readonly Option<int> SimilarityBar = new(
        ["--duplicate-similar", "-ds"],
        () => 96,
        "Similarity bar. This value means two image are considered as duplicates if their similarity is greater than it. Setting too small may cause different images to be considered as duplicates. Suggested values: [96-100]");

    public static readonly Option<bool> RecursiveOption = new(
        ["--recursive", "-r"],
        () => false,
        "Recursively search for similar images in subdirectories. (.trash content will be ignored)");

    public static readonly Option<KeepPreference[]> KeepOption = new(
        ["--keep", "-k"],
        () =>
        [
            KeepPreference.Colorful, KeepPreference.HighestResolution, KeepPreference.Largest, KeepPreference.Newest
        ],
        "Preference for sorting images by quality to determine which to keep when duplicates are found. Available options: Colorful|GrayScale|Newest|Oldest|Smallest|Largest|HighestResolution|LowestResolution.");

    public static readonly Option<DuplicateAction> ActionOption = new(
        ["--action", "-a"],
        () => DuplicateAction.MoveToTrash,
        "Action to take when duplicates are found. Available options: Nothing, Delete, MoveToTrash.");

    public static readonly Option<bool> YesOption = new(
        ["--yes", "-y"],
        () => false,
        "No interactive mode. Taking action without asking for confirmation.");

    public static readonly Option<string[]> ExtensionsOption = new(
        ["--extensions", "-e"],
        () => ["jpg", "jpeg", "png", "jfif"],
        "Extensions of files to dedup.");

    public static readonly Option<int> ThreadsOption = new(
        ["--threads", "-t"],
        () => Environment.ProcessorCount,
        $"Number of threads to use for image indexing. Default is {Environment.ProcessorCount}.");
}