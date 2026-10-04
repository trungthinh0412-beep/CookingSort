using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class LevelAddressablesSetup
{
    private const string LevelFolder = "Assets/_Project/Levels";
    private const string GroupName = "Levels";

    [InitializeOnLoadMethod]
    private static void ScheduleSync()
    {
        EditorApplication.delayCall += SyncLevelEntries;
    }

    [MenuItem("Tools/Addressables/Sync Level Prefabs")]
    public static void SyncLevelEntries()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.IsValidFolder(LevelFolder))
        {
            return;
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            Debug.LogError("[LevelAddressablesSetup] Khong tao duoc AddressableAssetSettings.");
            return;
        }

        AddressableAssetGroup group = settings.FindGroup(GroupName);
        bool changed = false;

        if (group == null)
        {
            group = settings.CreateGroup(
                GroupName,
                false,
                false,
                false,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
            changed = true;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { LevelFolder }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string address = Path.GetFileNameWithoutExtension(assetPath);
            AddressableAssetEntry entry = settings.FindAssetEntry(guid);

            if (entry == null || entry.parentGroup != group)
            {
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
                changed = true;
            }

            if (entry.address != address)
            {
                entry.SetAddress(address, false);
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelAddressablesSetup] Da dong bo prefab trong {LevelFolder} vao group {GroupName}.");
    }
}
