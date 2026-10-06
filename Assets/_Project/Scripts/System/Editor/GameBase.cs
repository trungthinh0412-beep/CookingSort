using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public class GameBase : EditorWindow
{
    void OnGUI()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.EndHorizontal();
    }

    [MenuItem("GameBase/Open Scene/Loading Scene %F1")]
    public static void OpenLoadingScene(){
        EditorSceneManager.OpenScene($"Assets/_Project/Scenes/LoadingScene.unity");
        Debug.Log($"<color=Green>Change scene succeed</color>");
    }

    [MenuItem("GameBase/Open Scene/Gameplay Scene %F2")]
    public static void OpenGamePlayScene(){
        EditorSceneManager.OpenScene($"Assets/_Project/Scenes/GameplayScene.unity");
        Debug.Log($"<color=Green>Change scene succeed</color>");
    }
    
    [MenuItem("GameBase/Debug/Clear Data")]
    public static void ClearData()
    {
        Data.ClearData();
    }

    [MenuItem("GameBase/Debug/Add 100k Money")]
    public static void Add100kMoney()
    {
        Data.PlayerData.CurrentGold += 100000;
        Debug.Log($"<color=Green>Add 100k coin succeed</color>");
    }

    [MenuItem("GameBase/Debug/Add 100 Build Gems")]
    public static void Add100BuildGems()
    {
        if (Data.PlayerData == null)
        {
            Debug.LogWarning("PlayerData is not loaded. Enter Play Mode first.");
            return;
        }

        Data.PlayerData.CurrentStar += 100;
        Data.SaveData();
        Debug.Log("<color=Green>Add 100 build gems succeed</color>");
    }

    [MenuItem("GameBase/Debug/Booster")]
    public static void AddBooster()
    {
        if (Data.PlayerData == null)
        {
            Debug.LogWarning("PlayerData is not loaded. Enter Play Mode first.");
            return;
        }

        // =

        //
        {

        }

        Data.SaveData();
        Debug.Log("<color=Green>Added 1 to each active PopupBooster quantity badge.</color>");
    }
    
    [MenuItem("GameBase/Debug/Switch Debug %`")]
    public static void SwitchDebug()
    {
        string path = "Assets/_Project/Config/GameConfig.asset";
        GameConfig gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
        gameConfig.isTesting = !gameConfig.isTesting;
        EditorUtility.SetDirty(gameConfig);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"<color=Green>Switched debug mode to: {gameConfig.isTesting}</color>");
    }
}
