using NUnit.Framework;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin.Tests;

public class CommandLineTests
{
    [Test]
    public void NoCommandsRequestNothing()
    {
        var options = CommandLine.Parse(new[] { "valheim_server", "-world", "Dedicated", "-port", "2456" });

        Assert.That(options.worldgenGuid, Is.Null);
    }

    [Test]
    public void GuidIsReadAmongVanillaArguments()
    {
        var options = CommandLine.Parse(new[] { "-world", "Dedicated", "-wh", "guid", "dev.test.labelled", "-port", "2456" });

        Assert.That(options.worldgenGuid, Is.EqualTo("dev.test.labelled"));
    }

    [Test]
    public void PrefixAndCommandIgnoreCaseButTheGuidDoesNot()
    {
        var options = CommandLine.Parse(new[] { "-WH", "GUID", "Dev.Test" });

        Assert.That(options.worldgenGuid, Is.EqualTo("Dev.Test"));
    }

    [Test]
    public void RepeatingTheSameGuidIsAllowed()
    {
        var options = CommandLine.Parse(new[] { "-wh", "guid", "a", "-wh", "guid", "a" });

        Assert.That(options.worldgenGuid, Is.EqualTo("a"));
    }

    [Test]
    public void SettingsAreTakenAsOneOpaqueValue()
    {
        var options = CommandLine.Parse(new[] { "-wh", "guid", "a", "-wh", "settings", "preset builtin.amur -x 'y'" });

        Assert.That(options.worldgenGuid, Is.EqualTo("a"));
        Assert.That(options.settings, Is.EqualTo("preset builtin.amur -x 'y'"));
    }

    [Test]
    public void SettingsMayBeEmpty()
    {
        var options = CommandLine.Parse(new[] { "-wh", "guid", "a", "-wh", "settings", "" });

        Assert.That(options.settings, Is.EqualTo(""));
    }

    [TestCase("-wh")]
    [TestCase("-wh", "guid")]
    [TestCase("-wh", "nonsense", "a")]
    [TestCase("-wh", "guid", "a", "-wh", "guid", "b")]
    [TestCase("-wh", "guid", "a", "-wh", "settings", "x", "-wh", "settings", "y")]
    [TestCase("-wh", "settings", "x")]
    public void MalformedCommandsAbortStartup(params string[] args)
    {
        Assert.Throws<ServerStartupException>(() => CommandLine.Parse(args));
    }
}
