using NUnit.Framework;
using Worldhalla.API;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin.Tests;

[WorldhallaPlugin]
internal sealed class LabelledWorldgen : IWorldGenerator
{
    public const string GUID = "dev.test.labelled";

    public string Guid => GUID;

    public string Moniker => "LABELLED";

    public string Name => "Labelled";

    public IWorldGenerator.Feature[] Features => System.Array.Empty<IWorldGenerator.Feature>();

    public ISettings DefaultSettings => new TestSettings("default");

    public ISettings DeserializeSettings(byte[] data, out string errorMessage)
    {
        errorMessage = null;

        return new TestSettings(System.Text.Encoding.UTF8.GetString(data));
    }

    public IWorldState Generate(int seed, ISettings settings) => throw new System.NotImplementedException();
}

internal sealed class UnlabelledWorldgen : IWorldGenerator
{
    public const string GUID = "dev.test.unlabelled";

    public string Guid => GUID;

    public string Moniker => "UNLABELLED";

    public string Name => "Unlabelled";

    public IWorldGenerator.Feature[] Features => System.Array.Empty<IWorldGenerator.Feature>();

    public ISettings DefaultSettings => new TestSettings("default");

    public ISettings DeserializeSettings(byte[] data, out string errorMessage) => throw new System.NotImplementedException();

    public IWorldState Generate(int seed, ISettings settings) => throw new System.NotImplementedException();
}

[WorldhallaPlugin]
internal sealed class ThrowingWorldgen : IWorldGenerator
{
    public const string GUID = "dev.test.throwing";

    public ThrowingWorldgen() => throw new System.Exception("this mod is broken");

    public string Guid => GUID;

    public string Moniker => "THROWING";

    public string Name => "Throwing";

    public IWorldGenerator.Feature[] Features => System.Array.Empty<IWorldGenerator.Feature>();

    public ISettings DefaultSettings => throw new System.NotImplementedException();

    public ISettings DeserializeSettings(byte[] data, out string errorMessage) => throw new System.NotImplementedException();

    public IWorldState Generate(int seed, ISettings settings) => throw new System.NotImplementedException();
}

[WorldhallaPlugin]
internal sealed class LabelledNonGenerator
{
}

/*
 * Discovery runs once per assembly for the life of the process, so these assert only what holds
 * however the suite is ordered. The fixture stands in for WorldhallaPlugin.Start, which is the only
 * thing that runs discovery in the game.
 */
public class RegistryTests
{
    [SetUp]
    public void Setup()
    {
        if (ValheimInstallation.Available)
            Registry.DiscoverGenerators();
    }

    [Test, RequiresValheim]
    public void LabelledGeneratorIsDiscovered()
    {
        Assert.That(Registry.FindByGuid(LabelledWorldgen.GUID, out var generator), Is.True);
        Assert.That(generator, Is.InstanceOf<LabelledWorldgen>());
    }

    /*
     * The interface alone must not be enough to register: a mod carries helper implementations and
     * test doubles it never meant to hand over.
     */
    [Test, RequiresValheim]
    public void UnlabelledGeneratorIsNotDiscovered()
    {
        Assert.That(Registry.FindByGuid(UnlabelledWorldgen.GUID, out _), Is.False);
    }

    [Test, RequiresValheim]
    public void RescanningDoesNotRegisterASecondInstance()
    {
        Registry.FindByGuid(LabelledWorldgen.GUID, out var first);

        Registry.DiscoverGenerators();

        Registry.FindByGuid(LabelledWorldgen.GUID, out var second);

        Assert.That(second, Is.SameAs(first));
    }

    /*
     * A labelled type that will not construct, or is not a generator at all, is someone else's bug.
     * It must not cost every other mod in the process its registration.
     */
    [Test, RequiresValheim]
    public void BrokenGeneratorsDoNotStopDiscovery()
    {
        Assert.DoesNotThrow(() => Registry.DiscoverGenerators());

        Assert.That(Registry.FindByGuid(ThrowingWorldgen.GUID, out _), Is.False);
        Assert.That(Registry.FindByGuid(LabelledWorldgen.GUID, out _), Is.True);
    }
}
