using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NiBot.Dedup.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Dedup;

public class Startup : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<DedupEngine>();
        services.AddScoped<ImageHasher>();
        services.AddTaskCanon();
    }
}