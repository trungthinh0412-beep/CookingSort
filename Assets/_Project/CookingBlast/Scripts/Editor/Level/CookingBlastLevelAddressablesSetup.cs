using System.Text.RegularExpressions;
using CookingSort2D;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

internal static class CookingBlastLevelAddressablesSetup
{
    internal const string GroupName = "CookingBlast Levels";

    [InitializeOnLoadMethod]
    private static void ScheduleSync()
    {
        EditorApplication.delayCall += SyncLevelEntries;
    }

    [MenuItem("Tools/Cooking Blast/Sync Level Addressables")]
    internal static void SyncLevelEntries()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.IsValidFolder(CookingBlastPrefabSetup.LevelDirectory))
            return;

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null) return;

        AddressableAssetGroup group = settings.FindGroup(GroupName);
        bool changed = false;
        if (group == null)
        {
            group = settings.CreateGroup(GroupName, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            changed = true;
        }

        var validGuids = new System.Collections.Generic.HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CookingBlastPrefabSetup.LevelDirectory }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!Regex.IsMatch(name, @"^Level [1-9]\d*$")) continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CookingBlastLevel level = prefab != null ? prefab.GetComponent<CookingBlastLevel>() : null;
            if (level == null || !level.TryValidate(out _)) continue;

            validGuids.Add(guid);
            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            if (entry == null || entry.parentGroup != group)
            {
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
                changed = true;
            }

            string address = "CookingBlast/" + name;
            if (entry.address != address)
            {
                entry.SetAddress(address, false);
                changed = true;
            }
        }

        var toRemove = new System.Collections.Generic.List<string>();
        foreach (AddressableAssetEntry entry in group.entries)
            if (!validGuids.Contains(entry.guid)) toRemove.Add(entry.guid);
        foreach (string guid in toRemove)
        {
            settings.RemoveAssetEntry(guid);
            changed = true;
        }

        if (!changed) return;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CookingBlast] Synced valid level prefabs to Addressables group '{GroupName}'.");
    }

    internal static void UseAssetDatabaseForPlaytest()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null) return;

        for (int i = 0; i < settings.DataBuilders.Count; i++)
        {
            if (!(settings.DataBuilders[i] is BuildScriptFastMode)) continue;
            if (settings.ActivePlayModeDataBuilderIndex != i)
                settings.ActivePlayModeDataBuilderIndex = i;
            return;
        }

        Debug.LogWarning("[CookingBlast] Addressables Fast Mode is missing. " +
                         "Playtest may load stale bundled level data.");
    }
}
