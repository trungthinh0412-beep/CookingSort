using UnityEditor;
using UnityEngine;
using TMPro;

[InitializeOnLoad]
public class ReplacePopupUI
{
    static ReplacePopupUI()
    {
        EditorApplication.delayCall += Execute;
    }

    public static void Execute()
    {
        if (SessionState.GetBool("PopupTrayFullReplaced_v2", false)) return;

        string preInGamePath = "Assets/_Project/Prefabs/Popup/PopupPreInGame.prefab";
        string trayFullPath = "Assets/_Project/Prefabs/Popup/PopupTrayFull.prefab";

        GameObject preInGamePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(preInGamePath);
        GameObject trayFullPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(trayFullPath);

        if (preInGamePrefab == null || trayFullPrefab == null) return;

        GameObject instTrayFull = (GameObject)PrefabUtility.InstantiatePrefab(trayFullPrefab);
        GameObject instPreInGame = (GameObject)PrefabUtility.InstantiatePrefab(preInGamePrefab);

        // Delete old UI
        Transform oldFrame = instTrayFull.transform.Find("PopupPanel");
        if (oldFrame != null) GameObject.DestroyImmediate(oldFrame.gameObject);
        
        Transform oldContainer = instTrayFull.transform.Find("Container");
        if (oldContainer != null) GameObject.DestroyImmediate(oldContainer.gameObject);

        Transform oldNewFrame = instTrayFull.transform.Find("Frame");
        if (oldNewFrame != null) GameObject.DestroyImmediate(oldNewFrame.gameObject);

        Transform oldBtn = instTrayFull.transform.Find("Button_Okay");
        if (oldBtn != null) GameObject.DestroyImmediate(oldBtn.gameObject);

        // Clones
        Transform newFrame = instPreInGame.transform.Find("Frame");
        if (newFrame != null)
        {
            GameObject clonedFrame = GameObject.Instantiate(newFrame.gameObject, instTrayFull.transform);
            clonedFrame.name = "Frame";
            
            TextMeshProUGUI[] texts = clonedFrame.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var txt in texts)
            {
                if (txt.text.Contains("Watch")) txt.text = "Board Is Full!";
                if (txt.text.Contains("5")) txt.gameObject.SetActive(false);
            }
        }
        
        Transform newBtn = instPreInGame.transform.Find("Button_ExtraMove");
        CustomButton clonedCustomBtn = null;
        if (newBtn != null)
        {
            GameObject clonedBtn = GameObject.Instantiate(newBtn.gameObject, instTrayFull.transform);
            clonedBtn.name = "Button_Okay";
            
            Transform icon = clonedBtn.transform.Find("Icon_Move_plus");
            if (icon != null) icon.gameObject.SetActive(false);
            
            TextMeshProUGUI[] texts = clonedBtn.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var txt in texts)
            {
                txt.text = "OK";
            }
            
            clonedCustomBtn = clonedBtn.GetComponent<CustomButton>();
        }

        // Link the button to the PopupTrayFull script field okButton
        PopupTrayFull scriptCmp = instTrayFull.GetComponent<PopupTrayFull>();
        if (scriptCmp != null && clonedCustomBtn != null)
        {
            var so = new SerializedObject(scriptCmp);
            var prop = so.FindProperty("okButton");
            if (prop != null)
            {
                prop.objectReferenceValue = clonedCustomBtn;
                so.ApplyModifiedProperties();
            }
        }

        PrefabUtility.SaveAsPrefabAsset(instTrayFull, trayFullPath);

        GameObject.DestroyImmediate(instTrayFull);
        GameObject.DestroyImmediate(instPreInGame);

        SessionState.SetBool("PopupTrayFullReplaced_v2", true);
        Debug.Log("Successfully replaced Frame and Button in PopupTrayFull v2");
    }
}
