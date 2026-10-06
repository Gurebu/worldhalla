using Worldhalla.API.Settings;
using Worldhalla.Plugin.Core;
using Worldhalla.Plugin.Core.Extensions;
using Worldhalla.Plugin.UI.Preview.Vanilla;

namespace Worldhalla.Plugin.UI.Preview;

// A preview's private WorldGenerator. A Worldhalla world is attached to it the way a played world is, so the
// WorldGenerator patches answer for it
public sealed class PreviewGeneration
{
    private readonly WorldGenerationTask? generation;

    private PreviewGeneration(WorldGenerationTask? generation, Task<WorldGenerator> completion)
    {
        this.generation = generation;
        Completion = completion;
    }

    public Task<WorldGenerator> Completion { get; }

    public float? Progress => generation?.Progress;

    // Must be called on the main thread: WorldGenerator's constructor seeds through UnityEngine.Random
    public static PreviewGeneration Start(RegisteredGenerator? worldgen, string seed, ISettings? settings)
    {
        WorldGenerator generator;
        try
        {
            generator = CreateGenerator(seed);
        }
        catch (Exception e)
        {
            return new PreviewGeneration(null, Task.FromException<WorldGenerator>(e));
        }

        if (worldgen == null)
            return new PreviewGeneration(null, PlaceRivers(generator));

        var generation = WorldGenerationTask.Start(worldgen, generator.m_world.m_seed, settings);
        return new PreviewGeneration(generation, Attach(generation, generator));
    }

    // Vanilla generation runs to the end
    public void Cancel()
    {
        generation?.Cancel();
    }

    // Constructing as a menu world skips river placement there, which then runs on a preview thread through
    // VanillaRiverPlacement
    private static WorldGenerator CreateGenerator(string seed)
    {
        var world = new World("", seed) { m_menu = true };
        var generator = new WorldGenerator(world);
        world.m_menu = false;
        return generator;
    }

    private static Task<WorldGenerator> PlaceRivers(WorldGenerator generator)
    {
        if (!VanillaRiverPlacement.Prepare())
            return Task.FromResult(generator);

        return Task.Run(() =>
        {
            VanillaRiverPlacement.Pregenerate(generator);
            return generator;
        });
    }

    private static async Task<WorldGenerator> Attach(WorldGenerationTask generation, WorldGenerator generator)
    {
        generator.SetState(await generation.Completion.ConfigureAwait(false));
        return generator;
    }
}
