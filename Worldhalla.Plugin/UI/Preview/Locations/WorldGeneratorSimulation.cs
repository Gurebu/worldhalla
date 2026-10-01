namespace Worldhalla.Plugin.UI.Preview.Locations;

// Static WorldGenerator methods find their world through WorldGenerator.instance. While a simulation runs on a thread,
// they find the simulated generator instead
internal static class WorldGeneratorSimulation
{
    [ThreadStatic] private static WorldGenerator? simulated;

    private sealed class Scope(WorldGenerator? previous) : IDisposable
    {
        public void Dispose() => simulated = previous;
    }

    public static WorldGenerator? Current => simulated ?? WorldGenerator.instance;

    public static IDisposable Use(WorldGenerator generator)
    {
        var scope = new Scope(simulated);
        simulated = generator;
        return scope;
    }
}
