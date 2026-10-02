namespace Worldhalla.Plugin.UI.Preview.Vanilla;

// Stands in for UnityEngine.Random inside the preview's copies of vanilla river placement, and nowhere else.
// Each preview thread gets its own generator, so previews never touch the engine's global random state.
public static class PreviewRandom
{
    [ThreadStatic] private static UnityRandom? current;
    [ThreadStatic] private static Stack<(uint, uint, uint, uint)>? saved;

    private sealed class Scope : IDisposable
    {
        public void Dispose()
        {
            current = null;
            saved = null;
        }
    }

    public static IDisposable Use(UnityRandom random)
    {
        if (current != null)
            throw new InvalidOperationException("A preview random generator is already in use on this thread");

        current = random;
        saved = new Stack<(uint, uint, uint, uint)>();
        return new Scope();
    }

    private static UnityRandom Current => current
        ?? throw new InvalidOperationException("PreviewRandom was called outside of a preview");

    // The engine's State can't be constructed from outside, so the copied code carries an empty one around
    // while the real state is kept here; vanilla always restores what it saved in the same method.
    public static UnityEngine.Random.State GetState()
    {
        saved!.Push(Current.State);
        return default;
    }

    public static void SetState(UnityEngine.Random.State _)
    {
        Current.State = saved!.Pop();
    }

    public static void InitState(int seed)
    {
        Current.InitState(seed);
    }

    public static float Range(float min, float max)
    {
        return Current.Range(min, max);
    }

    public static int Range(int min, int max)
    {
        return Current.Range(min, max);
    }
}
