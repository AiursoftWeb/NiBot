using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.NiBot.Calendar;
using Aiursoft.NiBot.Dedup;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
    .WithFeature(new CalendarHandler())
    .WithFeature(new DedupHandler())
    .RunAsync(args);

