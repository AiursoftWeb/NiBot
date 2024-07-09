using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NiBot.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Dedup;

public class Startup : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<DedupEngine>();
        services.AddTransient<ImageHasher>();
        services.AddTransient<BestPhotoSelector>();
        services.AddTransient<FilesHelper>();
        services.AddTaskCanon();
    }
}