using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.NiBot.Dedup;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
    .WithFeature(new DedupHandler())
    .WithFeature(new DedupCopyHandler())
    .WithFeature(new CompareHandler())
    .RunAsync(args);

