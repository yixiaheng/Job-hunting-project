using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class GridLayoutSet : MonoBehaviour
{
    [SerializeField] private float minimumCellWidth = 160f;
    private GridLayoutGroup grid;

    private void OnEnable() { Resize(); }
    private void OnRectTransformDimensionsChange() { Resize(); }

    private void Resize()
    {
        if (grid == null) 
            grid = GetComponent<GridLayoutGroup>();
        if (grid == null) 
            return;
        float width = ((RectTransform)transform).rect.width - grid.padding.horizontal;
        if (width <= 0f) return;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width + grid.spacing.x) / (minimumCellWidth + grid.spacing.x)));
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        float cellWidth = (width - grid.spacing.x * (columns - 1)) / columns;
        grid.cellSize = new Vector2(cellWidth, cellWidth * 1.15f);
    }
}
