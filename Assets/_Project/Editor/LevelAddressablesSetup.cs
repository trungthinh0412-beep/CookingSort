using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class LevelAddressablesSetup
{
    private const string LevelFolder = "Assets/_Project/Hidden Object/Prefabs/Level";
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
            // Only playable levels. Reusable spots/templates must never become level addresses.
            if (!System.Text.RegularExpressions.Regex.IsMatch(address, @"^Level [1-9]\d*$"))
                continue;
            var level = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (level == null || level.GetComponent<MagicSoft.Differences.DifferenceWorldBoard>() == null)
                continue;
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

        // Archived Loop Sort assets are preserved, but are no longer playable addresses.
        foreach (var existingGroup in settings.groups)
        {
            if (existingGroup == null) continue;
            var archived = new System.Collections.Generic.List<string>();
            foreach (var entry in existingGroup.entries)
                if (entry.AssetPath.StartsWith("Assets/_Project/Legacy/LoopSort/Levels/")) archived.Add(entry.guid);
            foreach (var guid in archived) { settings.RemoveAssetEntry(guid); changed = true; }
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
