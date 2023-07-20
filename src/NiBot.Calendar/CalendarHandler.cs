using System.CommandLine;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NiBot.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Calendar;

public class CalendarHandler : CommandHandler
{
    public override string Name => "calendar";

    public override string Description => "Show calendar.";

    public override void OnCommandBuilt(Command command)
    {
        command.SetHandler(Execute, CommonOptionsProvider.VerboseOption);
    }

    private Task Execute(bool verbose)
    {
        var services = ServiceBuilder
            .BuildServices<Startup>(verbose)
            .BuildServiceProvider();
        
        var calendar = services.GetRequiredService<CalendarRenderer>();
        calendar.Render();
        return Task.CompletedTask;
    }
}