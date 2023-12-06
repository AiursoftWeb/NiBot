using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Extensions;
using Aiursoft.NiBot.Calendar;
using Aiursoft.NiBot.Core;

return await new AiursoftCommandApp()
    .Configure(command =>
    {
        command
            .AddGlobalOptions()
            .AddPlugins(
                new CalendarPlugin()
            );
    })
    .RunAsync(args);
