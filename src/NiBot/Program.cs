using System.CommandLine;
using System.Reflection;
using Aiursoft.CommandFramework.Extensions;
using Aiursoft.NiBot.Calendar;
using Aiursoft.NiBot.Core;

var descriptionAttribute = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;

var program = new RootCommand(descriptionAttribute ?? "Unknown usage.")
    .AddGlobalOptions()
    .AddPlugins(
        new CalendarPlugin()
    );

return await program.InvokeAsync(args);
