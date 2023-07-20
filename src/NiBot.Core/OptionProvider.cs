using System.CommandLine;
using Aiursoft.CommandFramework.Models;

namespace Aiursoft.NiBot.Core;

public static class OptionsProvider
{
    public static RootCommand AddGlobalOptions(this RootCommand command)
    {
        var options = new Option[]
        {
            CommonOptionsProvider.DryRunOption,
            CommonOptionsProvider.VerboseOption
        };
        foreach (var option in options)
        {
            command.AddGlobalOption(option);
        }
        return command;
    }
}
