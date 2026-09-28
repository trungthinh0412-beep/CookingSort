using MagicSoft.Differences;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(DifferenceSpotView))]
public sealed class DifferenceSpotEditor : Editor
{
    private readonly BoxBoundsHandle handle = new BoxBoundsHandle();
    private bool previousToolsHidden;
    private void OnEnable() { previousToolsHidden = Tools.hidden; Tools.hidden = true; }
    private void OnDisable() { Tools.hidden = previousToolsHidden; }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DifferenceLevelEditing.SyncMatchingAreas = EditorGUILayout.ToggleLeft(
            "Sync matching A/B area while editing", DifferenceLevelEditing.SyncMatchingAreas);
        EditorGUILayout.HelpBox("Move/resize with the green Scene handles. With sync enabled, the matching spot on the other picture follows. The BoxCollider2D and marker follow Normalized Bounds automatically.", MessageType.Info);
    }
    private void OnSceneGUI()
    {
        var spot = (DifferenceSpotView)target;
        if (spot.Picture == null || spot.Picture.sprite == null) return;
        var spriteBounds = spot.Picture.sprite.bounds;
        var area = spot.NormalizedBounds;
        using (new Handles.DrawingScope(Color.green, spot.Picture.transform.localToWorldMatrix))
        {
            handle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;
            handle.center = (Vector2)spriteBounds.min + Vector2.Scale(area.center, spriteBounds.size);
            handle.size = Vector2.Scale(area.size, spriteBounds.size);
            EditorGUI.BeginChangeCheck();
            handle.DrawHandle();
            Vector3 center = Handles.Slider2D(handle.center, Vector3.forward, Vector3.right, Vector3.up,
                .08f, Handles.RectangleHandleCap, Vector2.zero);
            if (EditorGUI.EndChangeCheck())
            {
                Vector2 size = new Vector2(handle.size.x / spriteBounds.size.x, handle.size.y / spriteBounds.size.y);
                Vector2 normalizedCenter = new Vector2((center.x - spriteBounds.min.x) / spriteBounds.size.x,
                    (center.y - spriteBounds.min.y) / spriteBounds.size.y);
                var panel = spot.GetComponentInParent<DifferencePanelView>();
                var board = spot.GetComponentInParent<DifferenceLevelController>();
                int index = DifferenceLevelEditing.FindPairIndex(board, spot);
                var next = new Rect(normalizedCenter - size / 2, size);
                if (index >= 0)
                    DifferenceLevelEditing.SetPairBounds(board, index, spot == DifferenceLevelEditing.Spot(board, index, true),
                        next, DifferenceLevelEditing.SyncMatchingAreas);
                else
                    DifferenceLevelEditing.SetBounds(spot, panel, next);
            }
        }
    }
}
