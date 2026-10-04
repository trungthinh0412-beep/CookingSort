using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardBurnEffect))]
public sealed class CardBurnEffectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);

        CardBurnEffect effect = (CardBurnEffect)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Preview Burn"))
                effect.PlayBurn();
            if (GUILayout.Button("Reset Burn"))
                effect.ResetEffect();
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode, select a card in the Hierarchy, then click " +
                "Preview Burn. The component is added to cards automatically.",
                MessageType.Info
            );
        }
    }
}
