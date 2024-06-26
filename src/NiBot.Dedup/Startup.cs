using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Microsoft.Extensions.DependencyInjection;
using NiBot.Dedup.Services;

namespace NiBot.Dedup;

public class Startup : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<DedupEngine>();
        services.AddScoped<ImageHasher>();
        services.AddTaskCanon();
    }
}