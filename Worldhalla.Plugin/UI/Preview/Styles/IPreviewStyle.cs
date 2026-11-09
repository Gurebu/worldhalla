using UnityEngine;

namespace Worldhalla.Plugin.UI.Preview.Styles;

// Paint may read the grid one row above and below the painted one, and nothing further
public interface IPreviewStyle
{
    Color Paint(PreviewGrid grid, int i, int row);
}
