using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class FixSpineShaders {
    [MenuItem("Tools/Fix Spine Animations for Build")]
    public static void Execute() {
        var graphicsSettings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        SerializedObject serializedObject = new SerializedObject(graphicsSettings);
        SerializedProperty arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

        string[] shadersToAdd = new string[] {
            "Spine/SkeletonGraphic",
            "Spine/Skeleton",
            "Spine/SkeletonGraphic (Tint Black)"
        };

        bool changed = false;
        foreach(var shaderName in shadersToAdd) {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) continue;

            bool hasShader = false;
            for (int i = 0; i < arrayProp.arraySize; i++) {
                var prop = arrayProp.GetArrayElementAtIndex(i);
                if (prop.objectReferenceValue == shader) {
                    hasShader = true;
                    break;
                }
            }

            if (!hasShader) {
                arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
                arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1).objectReferenceValue = shader;
                changed = true;
                Debug.Log($"Added {shaderName} to Always Included Shaders!");
            }
        }

        if (changed) {
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("Spine shaders fixed!");
        } else {
            Debug.Log("Spine shaders were already included.");
        }
    }
}
