using UnityEngine;
using UnityEngine.UI;

namespace Worldhalla.Plugin.UI;

// Explicit gamepad navigation over columns of rows, each row listed left to right. Disabled and hidden controls are
// stepped over, so it has to be reapplied whenever they change. Crossing to another column lands on its control
// nearest in height
public sealed class NavigationGrid(params Selectable[][][] columns)
{
    public void Apply()
    {
        for (var column = 0; column < columns.Length; column++)
        for (var row = 0; row < columns[column].Length; row++)
        for (var i = 0; i < columns[column][row].Length; i++)
        {
            var selectable = columns[column][row][i];
            if (selectable == null)
                continue;

            selectable.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = Sideways(column, row, i, -1),
                selectOnRight = Sideways(column, row, i, 1),
                selectOnUp = Vertical(column, row, i, -1),
                selectOnDown = Vertical(column, row, i, 1),
            };
        }
    }

    private Selectable? Sideways(int column, int row, int index, int step)
    {
        var cells = columns[column][row];
        for (var i = index + step; i >= 0 && i < cells.Length; i += step)
        {
            if (IsUsable(cells[i]))
                return cells[i];
        }

        var other = column + step;
        if (other < 0 || other >= columns.Length)
            return null;

        var height = cells[index].transform.position.y;
        return columns[other].SelectMany(cell => cell).Where(IsUsable)
            .OrderBy(cell => Mathf.Abs(cell.transform.position.y - height))
            .FirstOrDefault();
    }

    private Selectable? Vertical(int column, int row, int index, int step)
    {
        var rows = columns[column];
        for (var r = row + step; r >= 0 && r < rows.Length; r += step)
        {
            var cells = rows[r];
            var aligned = Mathf.Min(index, cells.Length - 1);
            if (IsUsable(cells[aligned]))
                return cells[aligned];
            if (cells.FirstOrDefault(IsUsable) is { } usable)
                return usable;
        }

        return null;
    }

    private static bool IsUsable(Selectable? selectable)
    {
        return selectable != null && selectable.gameObject.activeInHierarchy && selectable.IsInteractable();
    }
}
