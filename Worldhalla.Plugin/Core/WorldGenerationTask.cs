using UnityEngine;
using Worldhalla.API;
using Worldhalla.API.Settings;

namespace Worldhalla.Plugin.Core;

public sealed class WorldGenerationTask : IProgress<float>
{
    private readonly CancellationTokenSource cancellation = new();
    private float progress = -1f;

    private WorldGenerationTask(Func<WorldGenerationTask, Task<GeneratedWorld>> start)
    {
        Completion = start(this);
    }

    public Task<GeneratedWorld> Completion { get; }

    public float? Progress => Volatile.Read(ref progress) is var value and >= 0f ? value : null;

    public static WorldGenerationTask Start(IWorldGenerator worldgen, int seed, ISettings? settings)
    {
        return new WorldGenerationTask(generation => Task.Run(() =>
        {
            var token = generation.cancellation.Token;
            return GeneratedWorld.Of(worldgen, worldgen.Generate(seed, settings, generation, token));
        }));
    }

    public static WorldGenerationTask Of(Task<GeneratedWorld> task)
    {
        return new WorldGenerationTask(_ => task);
    }

    public void Cancel()
    {
        cancellation.Cancel();
    }

    public bool Wait(int millisecondsTimeout)
    {
        return ((IAsyncResult)Completion).AsyncWaitHandle.WaitOne(millisecondsTimeout);
    }

    public GeneratedWorld Wait()
    {
        return Completion.GetAwaiter().GetResult();
    }

    void IProgress<float>.Report(float value)
    {
        Volatile.Write(ref progress, Mathf.Clamp01(value));
    }
}
