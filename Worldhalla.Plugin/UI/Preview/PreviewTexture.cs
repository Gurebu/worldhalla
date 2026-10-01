using UnityEngine;

namespace Worldhalla.Plugin.UI.Preview;

internal sealed class PreviewTexture
{
    private object? uploaded;
    private int uploadedRevision;

    public readonly Texture2D texture;

    public PreviewTexture(int size)
    {
        texture = Shapes.NewTexture(size);
    }

    public void Show(PreviewRender render) => Show(render, render.PaintedRows, render.pixels);

    public void Show(DetailsRender render) => Show(render, render.Revision, render.pixels);

    private void Show(object render, int revision, Color32[] pixels)
    {
        if (render == uploaded && revision == uploadedRevision)
            return;

        uploaded = render;
        uploadedRevision = revision;
        texture.SetPixels32(pixels);
        texture.Apply(false);
    }
}
