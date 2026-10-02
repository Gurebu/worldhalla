using NUnit.Framework;
using Worldhalla.API;
using Worldhalla.API.Attributes;
using Worldhalla.API.Settings;
using Worldhalla.API.World;
using Worldhalla.Plugin.Core;

namespace Worldhalla.Plugin.Tests;

[WorldhallaPlugin(GUID)]
internal sealed class LabelledWorldgen : IWorldGenerator
{
    public const string GUID = "dev.test.labelled";

    public string Moniker => "LABELLED";

    public string Name => "Labelled";
    
    public string Version => "1.0.0";

    public IWorldGenerator.Feature[] Features => System.Array.Empty<IWorldGenerator.Feature>();

    public IWorldState Generate(int seed, ISettings settings, System.IProgress<float> progress, System.Threading.CancellationToken cancellation) => throw new System.NotImplementedException();
}

internal sealed class UnlabelledWorldgen : IWorldGenerator
{
    public const string GUID = "dev.test.unlabelled";

    public string Moniker => "UNLABELLED";

    public string Name => "Unlabelled";
    
    public string Version => "1.0.0";

    public IWorldGenerator.Feature[] Features => System.Array.Empty<IWorldGenerator.Feature>();

    public IWorldState Generate(int seed, ISettings settings, System.IProgress<float> progress, System.Threading.CancellationToken cancellation) => throw new System.NotImplementedException();
}

[WorldhallaPlugin(GUID)]
internal sealed class ThrowingWorldgen : IWorldGenerator
{
    public const string GUID = "dev.test.throwing";

    public ThrowingWorldgen() => throw new System.Exception("this mod is broken");

    public string Moniker => "THROWING";

    public string Name => "Throwing";
    
    public string Version => "1.0.0";

    public IWorldGenerator.Feature[] Features => System.Array.Empty<IWorldGenerator.Feature>();

    public IWorldState Generate(int seed, ISettings settings, System.IProgress<float> progress, System.Threading.CancellationToken cancellation) => throw new System.NotImplementedException();
}

[WorldhallaPlugin(" ")]
internal sealed class BlankGuidWorldgen : IWorldGenerator
{
    public string Moniker => "BLANK";

    public string Name => "Blank GUID";

    public string Version => "1.0.0";

    public IWorldGenerator.Feature[] Features => System.Array.Empty<IWorldGenerator.Feature>();

    public IWorldState Generate(int seed, ISettings settings, System.IProgress<float> progress, System.Threading.CancellationToken cancellation) => throw new System.NotImplementedException();
}

[WorldhallaPlugin(GUID)]
internal sealed class OversizedDefaultsWorldgen : IWorldGenerator, ISettingsHandler
{
    public const string GUID = "dev.test.oversized";

    public string Moniker => "OVERSIZED";

    public string Name => "Oversized defaults";

    public string Version => "1.0.0";

    public IWorldGenerator.Feature[] Features => System.Array.Empty<IWorldGenerator.Feature>();

    public ISettings DefaultSettings => new TestSettings(new string('x', SettingsBlob.MAX_SIZE + 1));

    public ISettings BuildSettingsFromBinaryBlob(byte[] data) => throw new System.NotImplementedException();

    public ISettings BuildSettingsFromCommandLine(string settings) => throw new System.NotImplementedException();

    public string CommandLineUsage => "";

    public ISettingsForm CreateForm(ISettings current) => throw new System.NotImplementedException();

    public IWorldState Generate(int seed, ISettings settings, System.IProgress<float> progress, System.Threading.CancellationToken cancellation) => throw new System.NotImplementedException();
}

[WorldhallaPlugin("dev.test.nongenerator")]
internal sealed class LabelledNonGenerator
{
}

/*
 * Discovery runs once per assembly for the life of the process, so these assert only what holds
 * however the suite is ordered. The fixture stands in for WorldhallaPlugin.Start and the dedicated
 * server's startup, which are what run discovery in the game.
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
    public void GeneratorWithOversizedDefaultsIsNotDiscovered()
    {
        Assert.That(Registry.FindByGuid(OversizedDefaultsWorldgen.GUID, out _), Is.False);
    }

    [Test, RequiresValheim]
    public void UnlabelledGeneratorIsNotDiscovered()
    {
        Assert.That(Registry.FindByGuid(UnlabelledWorldgen.GUID, out _), Is.False);
    }

    [Test, RequiresValheim]
    public void DiscoveredGeneratorIsKnownByItsLabel()
    {
        Assert.That(Registry.Generators.Keys, Does.Contain(LabelledWorldgen.GUID));
        Assert.That(Registry.Generators[LabelledWorldgen.GUID], Is.InstanceOf<LabelledWorldgen>());
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
        Assert.That(Registry.FindByGuid(" ", out _), Is.False);
        Assert.That(Registry.FindByGuid(LabelledWorldgen.GUID, out _), Is.True);
    }
}
