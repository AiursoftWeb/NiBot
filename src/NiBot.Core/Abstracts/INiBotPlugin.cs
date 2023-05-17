using Aiursoft.NiBot.Core.Framework;

namespace Aiursoft.NiBot.Core.Abstracts;

public interface INiBotPlugin
{
    public CommandHandler[] Install();
}
