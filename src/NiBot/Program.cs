using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using NiBot.Calendar;
using NiBot.Dedup;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
    .WithFeature(new CalendarHandler())
    .WithFeature(new DedupHandler())
    .RunAsync(args);

