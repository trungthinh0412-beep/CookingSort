using System.Collections.Generic;
using UnityEngine;

public enum TrayLayoutStartCorner
{
    UpperLeft,
    UpperRight,
    LowerLeft,
    LowerRight
}

[ExecuteAlways]
[DisallowMultipleComponent]
public class TrayLayoutGroup : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private bool autoLayout = true;
    [SerializeField, Min(1)] private int columnCount = 5;
    [SerializeField] private Vector2 spacing = new Vector2(2f, 4.2f);
    [SerializeField] private Vector2 offset;
    [SerializeField] private float singleRowOffsetY;
    [SerializeField] private float twoRowOffsetY = 0.15f;
    [SerializeField] private float twoRowUpperOffsetY = 0.4f;
    [SerializeField] private float twoRowLowerOffsetY = 0.25f;
    [SerializeField] private TextAnchor childAlignment = TextAnchor.MiddleCenter;
    [SerializeField] private TrayLayoutStartCorner startCorner = TrayLayoutStartCorner.LowerLeft;

    [Header("Child Position")]
    [Tooltip("Keep each Tray's current local Z instead of setting all of them to Child Z.")]
    [SerializeField] private bool preserveChildZ;
    [SerializeField] private float childZ;

    private readonly List<Transform> _trayChildren = new List<Transform>();
    private bool _isRebuilding;

    public bool AutoLayout => autoLayout;

    private void OnEnable()
    {
        if (autoLayout)
        {
            RebuildLayout();
        }
    }

    private void OnValidate()
    {
        columnCount = Mathf.Max(1, columnCount);
        spacing.x = Mathf.Max(0f, spacing.x);
        spacing.y = Mathf.Max(0f, spacing.y);

        if (autoLayout)
        {
            RebuildLayout();
        }
    }

    private void OnTransformChildrenChanged()
    {
        if (autoLayout)
        {
            RebuildLayout();
        }
    }

    [ContextMenu("Rebuild Layout")]
    public void RebuildLayout()
    {
        if (_isRebuilding) return;

        _isRebuilding = true;
        CollectTrayChildren();

        int childCount = _trayChildren.Count;
        if (childCount == 0)
        {
            _isRebuilding = false;
            return;
        }

        int columns = Mathf.Min(Mathf.Max(1, columnCount), childCount);
        int rows = Mathf.CeilToInt(childCount / (float)columns);
        float width = (columns - 1) * spacing.x;
        float height = (rows - 1) * spacing.y;
        float rowOffsetY = rows == 1
            ? singleRowOffsetY
            : rows == 2
                ? twoRowOffsetY
                : 0f;
        Vector2 firstPosition = GetFirstPosition(
            width,
            height,
            rowOffsetY
        );

        bool startOnRight = startCorner == TrayLayoutStartCorner.UpperRight ||
                            startCorner == TrayLayoutStartCorner.LowerRight;
        bool startOnBottom = startCorner == TrayLayoutStartCorner.LowerLeft ||
                             startCorner == TrayLayoutStartCorner.LowerRight;

        for (int index = 0; index < childCount; index++)
        {
            int column = index % columns;
            int row = index / columns;
            if (startOnRight) column = columns - 1 - column;
            if (startOnBottom) row = rows - 1 - row;

            Transform child = _trayChildren[index];
            Vector3 localPosition = child.localPosition;
            localPosition.x = firstPosition.x + column * spacing.x;
            localPosition.y = firstPosition.y - row * spacing.y;
            if (rows == 2 && row == 0)
            {
                localPosition.y += twoRowUpperOffsetY;
            }
            else if (rows == 2 && row == 1)
            {
                localPosition.y += twoRowLowerOffsetY;
            }
            if (!preserveChildZ) localPosition.z = childZ;
            child.localPosition = localPosition;
        }

        _isRebuilding = false;
    }

    private void CollectTrayChildren()
    {
        _trayChildren.Clear();

        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);
            if (child.GetComponentInChildren<CardSlotHolder>(true) != null)
            {
                _trayChildren.Add(child);
            }
        }
    }

    private Vector2 GetFirstPosition(
        float width,
        float height,
        float rowOffsetY)
    {
        float x = offset.x;
        float y = offset.y + rowOffsetY;

        switch (childAlignment)
        {
            case TextAnchor.UpperCenter:
            case TextAnchor.MiddleCenter:
            case TextAnchor.LowerCenter:
                x -= width * 0.5f;
                break;
            case TextAnchor.UpperRight:
            case TextAnchor.MiddleRight:
            case TextAnchor.LowerRight:
                x -= width;
                break;
        }

        switch (childAlignment)
        {
            case TextAnchor.MiddleLeft:
            case TextAnchor.MiddleCenter:
            case TextAnchor.MiddleRight:
                y += height * 0.5f;
                break;
            case TextAnchor.LowerLeft:
            case TextAnchor.LowerCenter:
            case TextAnchor.LowerRight:
                y += height;
                break;
        }

        return new Vector2(x, y);
    }
}
