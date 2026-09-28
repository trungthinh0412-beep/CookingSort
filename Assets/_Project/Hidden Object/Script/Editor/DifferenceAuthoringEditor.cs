using MagicSoft.Differences;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DifferenceLevelController))]
public sealed class DifferenceAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Open Find Differences Editor")) DifferenceLevelEditorWindow.OpenWindow();
        EditorGUILayout.HelpBox("Drop sprites into Picture A/B > Artwork > SpriteRenderer > Sprite. Select a Spot and use its Scene handles, or open the Level Editor. Orange outlines show world click areas.", MessageType.Info);
        if (GUILayout.Button("Add Difference Pair (A + B)")) AddPair();
        if (GUILayout.Button("Validate Level"))
        {
            var board = (DifferenceLevelController)target;
            if (board.TryValidate(out string error)) Debug.Log("[Differences] Level is valid.", board);
            else Debug.LogError("[Differences] " + error, board);
        }
    }

    private void AddPair()
    {
        var board = (DifferenceLevelController)target;
        try
        {
            int index = DifferenceLevelEditing.AddPair(board, new Rect(.4f, .4f, .2f, .2f));
            Selection.activeGameObject = DifferenceLevelEditing.Spot(board, index, true).gameObject;
        }
        catch (System.Exception exception) { Debug.LogError(exception.Message, board); }
    }
}
