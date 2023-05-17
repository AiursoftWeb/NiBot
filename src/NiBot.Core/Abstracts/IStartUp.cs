using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NiBot.Core.Abstracts;

public interface IStartUp
{
    public void ConfigureServices(IServiceCollection services);
}
