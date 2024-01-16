using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using NiBot.Calendar;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.DryRunOption)
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
    .WithFeature(new CalendarHandler())
    .RunAsync(args);

