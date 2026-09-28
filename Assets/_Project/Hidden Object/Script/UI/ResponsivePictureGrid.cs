using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public sealed class ResponsivePictureGrid : MonoBehaviour
{
    [SerializeField] private RectTransform viewport;
    [SerializeField, Min(1)] private int columns = 3;
    [SerializeField, Min(.1f)] private float heightOverWidth = 1.5f;
    private GridLayoutGroup grid;
    private float previousWidth = -1;
    private void OnEnable() { grid = GetComponent<GridLayoutGroup>(); previousWidth = -1; }
    private void LateUpdate()
    {
        if (viewport == null || grid == null || Mathf.Approximately(previousWidth, viewport.rect.width)) return;
        previousWidth = viewport.rect.width;
        float width = Mathf.Max(1, (previousWidth - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = new Vector2(width, width * heightOverWidth);
    }
}
