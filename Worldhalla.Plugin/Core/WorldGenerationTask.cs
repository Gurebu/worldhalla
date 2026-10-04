using UnityEngine;
using Worldhalla.API.Settings;

namespace Worldhalla.Plugin.Core;

public sealed class WorldGenerationTask : IProgress<float>
{
    private readonly CancellationTokenSource cancellation = new();
    private float progress = -1f;

    private WorldGenerationTask()
    {
    }

    public Task<WorldStateWrapper> Completion { get; private set; } = null!;

    public float? Progress => Volatile.Read(ref progress) is var value and >= 0f ? value : null;

    public static WorldGenerationTask Start(RegisteredGenerator generator, int seed, ISettings? settings)
    {
        var generation = new WorldGenerationTask();
        var token = generation.cancellation.Token;
        generation.Completion = Task.Run(() => WorldStateWrapper.Of(generator, generator.Generate(seed, settings, generation, token)));
        return generation;
    }

    public void Cancel()
    {
        cancellation.Cancel();
    }

    public bool Wait(int millisecondsTimeout)
    {
        return ((IAsyncResult)Completion).AsyncWaitHandle.WaitOne(millisecondsTimeout);
    }

    public WorldStateWrapper Wait()
    {
        return Completion.GetAwaiter().GetResult();
    }

    void IProgress<float>.Report(float value)
    {
        Volatile.Write(ref progress, Mathf.Clamp01(value));
    }
}
