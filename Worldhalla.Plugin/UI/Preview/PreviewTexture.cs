using UnityEngine;

namespace Worldhalla.Plugin.UI.Preview;

internal sealed class PreviewTexture
{
    private PreviewRender? uploaded;
    private int uploadedRows;

    public readonly Texture2D texture;

    public PreviewTexture(int size)
    {
        texture = Shapes.NewTexture(size);
    }

    public void Show(PreviewRender render)
    {
        if (render == uploaded && render.PaintedRows == uploadedRows)
            return;

        uploaded = render;
        uploadedRows = render.PaintedRows;
        texture.SetPixels32(render.pixels);
        texture.Apply(false);
    }
}
