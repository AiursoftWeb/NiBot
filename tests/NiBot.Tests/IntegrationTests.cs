using System.CommandLine;
using Aiursoft.CommandFramework.Extensions;
using Aiursoft.NiBot.Calendar;
using Aiursoft.NiBot.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NiBot.Tests;

[TestClass]
public class IntegrationTests
{
    private readonly RootCommand _program;

    public IntegrationTests()
    {
        _program = new RootCommand("Test env.")
            .AddGlobalOptions()
            .AddPlugins(new CalendarPlugin());
    }

    [TestMethod]
    public async Task InvokeHelp()
    {
        var result = await _program.InvokeAsync(new[] { "--help" });
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task InvokeVersion()
    {
        var result = await _program.InvokeAsync(new[] { "--version" });
        Assert.AreEqual(0, result);
    }
    
    [TestMethod]
    public async Task InvokeCalendar()
    {
        var result = await _program.InvokeAsync(new[] { "calendar", "-v" });
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task InvokeUnknown()
    {
        var result = await _program.InvokeAsync(new[] { "--wtf" });
        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public async Task InvokeWithoutArg()
    {
        var result = await _program.InvokeAsync(Array.Empty<string>());
        Assert.AreEqual(0, result);
    }
}
