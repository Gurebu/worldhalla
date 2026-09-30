using Worldhalla.API.World;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin.UI.Preview;

internal sealed class WorldPreview
{
    public enum Phase
    {
        Idle,
        Generating,
        Sampling,
        Done,
        Failed,
    }

    public const int DETAILS_SIZE = 1024;
    private const int PREVIEW_SUPERSAMPLING = 3;
    private const float DEFAULT_WORLD_RADIUS = 10500f;

    private readonly int previewSize;
    private int version;
    private Func<Task<GeneratedWorld>>? wanted;

    private Task<GeneratedWorld>? running;
    private int runningVersion;

    private GeneratedWorld? state;
    private Exception? failure;

    public PreviewRender? Main { get; private set; }
    public PreviewRender? Details { get; private set; }

    public WorldPreview(int previewSize)
    {
        this.previewSize = previewSize;
    }

    public Phase CurrentPhase
    {
        get
        {
            if (wanted == null)
                return Phase.Idle;
            if (failure != null || Main?.Error != null)
                return Phase.Failed;
            if (Main == null)
                return Phase.Generating;
            return Main.IsComplete ? Phase.Done : Phase.Sampling;
        }
    }

    public bool CanShowDetails => state != null;

    // A generation that is already running can't be interrupted; it finishes and its result is dropped.
    // Requests made meanwhile replace each other, so only the latest one starts afterwards.
    public void Request(Func<Task<GeneratedWorld>> createState)
    {
        DropOutputs();
        version++;
        wanted = createState;
    }

    public void Clear()
    {
        DropOutputs();
        version++;
        wanted = null;
    }

    public void Tick()
    {
        if (running is { IsCompleted: true })
        {
            if (runningVersion == version)
            {
                if (running.IsFaulted)
                    failure = running.Exception!.InnerException ?? running.Exception;
                else
                    state = running.Result;
            }

            running = null;
        }

        if (running == null && wanted != null && state == null && failure == null)
        {
            runningVersion = version;
            running = Start(wanted);
        }

        if (state != null && Main == null)
            Main = PreviewRender.Start(state, GetWorldRadius(state), previewSize, PREVIEW_SUPERSAMPLING, new SimplePreviewStyle());
    }

    public PreviewRender? EnsureDetails()
    {
        if (state != null && Details == null)
            Details = PreviewRender.Start(state, GetWorldRadius(state), DETAILS_SIZE, 1, new RichPreviewStyle());

        return Details;
    }

    public Exception? GetFailure()
    {
        return failure ?? Main?.Error;
    }

    private static Task<GeneratedWorld> Start(Func<Task<GeneratedWorld>> createState)
    {
        try
        {
            return createState();
        }
        catch (Exception e)
        {
            return Task.FromException<GeneratedWorld>(e);
        }
    }

    private void DropOutputs()
    {
        Main?.Cancel();
        Details?.Cancel();
        Main = null;
        Details = null;
        state = null;
        failure = null;
    }

    private static float GetWorldRadius(GeneratedWorld world)
    {
        return world.State is IWorldGeography geography ? geography.WorldSize / 2f : DEFAULT_WORLD_RADIUS;
    }
}
