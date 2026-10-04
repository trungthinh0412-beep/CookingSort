using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public class FixLockLevelInBooster
{
    static FixLockLevelInBooster()
    {
        EditorApplication.delayCall += Execute;
    }

    public static void Execute()
    {
        // removed check

        string[] paths = {
            "Assets/_Project/Prefabs/Popup/PopupBooster.prefab",
            "Assets/_Project/Prefabs/Popup/PopupBoosterQuit.prefab"
        };
        foreach(var path in paths) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PopupBooster script = inst.GetComponent<PopupBooster>();
            if (script == null) continue;
            
            var so = new SerializedObject(script);
            
            string[] slots = {"preLevelCardSlot1", "preLevelCardSlot2", "preLevelCardSlot3"};
            foreach(var slotStr in slots) {
                var slotProp = so.FindProperty(slotStr);
                if (slotProp != null) {
                    var lockLevelProp = slotProp.FindPropertyRelative("lockLevel");
                    var statusProp = slotProp.FindPropertyRelative("statusButton");
                    
                    if (statusProp != null && statusProp.objectReferenceValue != null) {
                        Component statusBtn = (Component)statusProp.objectReferenceValue;
                        // Search for Lock_level near the status button (usually siblings under the same card parent)
                        Transform cardParent = statusBtn.transform.parent;
                        Transform lockTrans = cardParent.Find("Lock_level");
                        // Sometimes it's a bit deeper or higher, let's just search children of the cardParent
                        if (lockTrans == null) {
                            Transform[] allChildren = cardParent.GetComponentsInChildren<Transform>(true);
                            foreach(var c in allChildren) {
                                if (c.name == "Lock_level") {
                                    lockTrans = c;
                                    break;
                                }
                            }
                        }
                        
                        if (lockTrans != null) {
                            lockLevelProp.objectReferenceValue = lockTrans.gameObject;
                        } else {
                            Debug.LogWarning($"Could not find Lock_level for {slotStr} in {path}");
                        }
                    }
                }
            }
            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(inst, path);
            GameObject.DestroyImmediate(inst);
        }

        SessionState.SetBool("FixLockLevelInBoosterDone", true);
        Debug.Log("Re-linked lockLevel in PopupBooster & PopupBoosterQuit!");
    }
}
