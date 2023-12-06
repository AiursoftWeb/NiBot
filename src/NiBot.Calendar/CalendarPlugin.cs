using Aiursoft.CommandFramework.Abstracts;

namespace Aiursoft.NiBot.Calendar;

public class CalendarPlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[]
        {
            new CalendarHandler(),
        };
    }
}