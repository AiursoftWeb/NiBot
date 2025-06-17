using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.NiBot.Dedup;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
    .WithFeature(new DedupHandler()) // Dedup handler will de-duplicate images in a directory.
    .WithFeature(new DedupCopyHandler()) // Dedup copy handler will copy images from source to destination without creating duplicates.
    .WithFeature(new DedupPatchHandler()) // Dedup patch handler will only copy duplicate and better quality images from source to destination.
    .WithFeature(new CompareHandler())
    .WithFeature(new DupTopHandler())
    .RunAsync(args);

