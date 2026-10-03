namespace Worldhalla.Plugin.UI.Preview;

public sealed class WorldPreview
{
    public enum Phase
    {
        Idle,
        Generating,
        Sampling,
        Done,
        Failed,
    }

    private const int PREVIEW_SUPERSAMPLING = 3;

    private readonly int previewSize;
    private int version;
    private Func<PreviewGeneration>? wanted;

    private PreviewGeneration? running;
    private int runningVersion;

    private WorldGenerator? generator;
    private Exception? failure;

    private PreviewCache? cache;
    private DetailsRender? wholeDetails;
    private DetailsRender? details;
    private DetailsRender? pendingDetails;

    public PreviewRender? Main { get; private set; }

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

    public bool CanShowDetails => generator != null;

    public WorldGenerator? Generator => generator;

    public float? WorldRadius => generator != null ? PreviewSampling.WorldRadius(generator) : null;

    public DetailsRender? WholeDetails => wholeDetails;

    public float? GenerationProgress => runningVersion == version ? running?.Progress : null;

    // Generations run one at a time: a cancelled one is left to wind down before the latest request starts.
    public void Request(Func<PreviewGeneration> startGeneration)
    {
        DropOutputs();
        version++;
        wanted = startGeneration;
    }

    public void Clear()
    {
        DropOutputs();
        version++;
        wanted = null;
    }

    public void Tick()
    {
        if (running?.Completion is { IsCompleted: true } completion)
        {
            if (runningVersion == version)
            {
                if (completion.Status == TaskStatus.RanToCompletion)
                    generator = completion.Result;
                else
                    failure = completion.Exception?.InnerException ?? (Exception?)completion.Exception ?? new OperationCanceledException();
            }

            running = null;
        }

        if (running == null && wanted != null && generator == null && failure == null)
        {
            runningVersion = version;
            running = wanted();
        }

        if (generator != null && Main == null)
            Main = PreviewRender.Start(generator, PreviewSampling.WorldRadius(generator), previewSize, PREVIEW_SUPERSAMPLING, new SimplePreviewStyle());
    }

    // The whole world is rendered once and kept, to show wherever a zoomed render has nothing yet. The shown render stays
    // up until the next one has painted what the cache holds. A view that changes meanwhile waits for that, which spares
    // restarting on every frame of a drag
    public DetailsRender? EnsureDetails(PreviewView view)
    {
        if (generator == null)
            return null;

        cache ??= new PreviewCache(PreviewSampling.WorldRadius(generator), PreviewView.CELLS);
        wholeDetails ??= DetailsRender.Start(generator, cache, PreviewView.Whole, DetailsRender.COARSEST_BLOCK, new HdrPreviewStyle());

        if (pendingDetails is { IsReady: true })
        {
            details = pendingDetails;
            pendingDetails = null;
        }

        if (pendingDetails == null && (details == null || !details.view.Equals(view)))
        {
            if (details != wholeDetails)
                details?.Cancel();

            if (view.IsWhole)
                details = wholeDetails;
            else
                pendingDetails = DetailsRender.Start(generator, cache, view, CoarsestBlock(view), new HdrPreviewStyle());
        }

        return details;
    }

    // Blocks as coarse as the whole world's pixels would only cover it up with something no better
    private int CoarsestBlock(PreviewView view)
    {
        return wholeDetails!.IsComplete
            ? Math.Max(1, PreviewView.MAX_ZOOM / (2 * view.cellsPerPixel))
            : DetailsRender.COARSEST_BLOCK;
    }

    public Exception? GetFailure()
    {
        return failure ?? Main?.Error ?? wholeDetails?.Error ?? details?.Error;
    }

    private void DropOutputs()
    {
        running?.Cancel();
        Main?.Cancel();
        wholeDetails?.Cancel();
        details?.Cancel();
        pendingDetails?.Cancel();
        Main = null;
        wholeDetails = null;
        details = null;
        pendingDetails = null;
        cache = null;
        generator = null;
        failure = null;
    }
}
