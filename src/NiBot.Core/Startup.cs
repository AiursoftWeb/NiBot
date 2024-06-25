using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Core;

public class Startup : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<CalendarRenderer>();
        services.AddScoped<DedupEngine>();
        services.AddScoped<ImageHasher>();
        services.AddTaskCanon();
    }
}