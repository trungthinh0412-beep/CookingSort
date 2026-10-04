using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Checks Collection assets and regressions using temporary objects and an isolated save file.</summary>
public static class CollectionValidation
{
    private const string ConfigPath = "Assets/_Project/Config/CollectionConfig.asset";
    private const string CollectionPath = "Assets/_Project/Prefabs/Popup/PopupCollection.prefab";
    private const string InfoPath = "Assets/_Project/Prefabs/Popup/PopupCollectionInfor.prefab";

    [MenuItem("Tools/Collection/Validate Assets and Progress")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run Collection validation outside Play Mode.");

        CollectionConfig config = AssetDatabase.LoadAssetAtPath<CollectionConfig>(ConfigPath);
        Require(config != null, "CollectionConfig is missing.");
        var errors = new List<string>();
        config.ValidateData(errors);
        Require(errors.Count == 0, string.Join("\n", errors));

        GameObject collectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionPath);
        GameObject infoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InfoPath);
        Require(collectionPrefab != null && infoPrefab != null, "Collection popup prefabs are missing.");
        Require(infoPrefab.GetComponents<Popup>().Length == 1, "Details prefab must have one Popup component.");
        Require(infoPrefab.GetComponent<PopupCollectionInfor>() != null, "Details script is missing.");
        Require(infoPrefab.GetComponent<PopupAvatar>() == null, "Details prefab contains PopupAvatar.");
        CheckReferences(infoPrefab.GetComponent<PopupCollectionInfor>(),
            "background", "container", "collectionConfig", "titleText", "coverImage", "progressText",
            "cardContent", "cardItemPrefab", "cardScrollRect", "rewardRoot", "rewardText", "emptyState");
        CheckReferences(collectionPrefab.GetComponent<PopupCollection>(),
            "collectionConfig", "collectionContent", "collectionButtonTemplate", "unlockPopup", "lockPopup");
        PopupConfig popupConfig = AssetDatabase.LoadAssetAtPath<PopupConfig>("Assets/_Project/Config/PopupConfig.asset");
        Require(popupConfig.popups.Count(p => p is PopupCollectionInfor) == 1,
            "PopupConfig must register one shared Collection details popup.");

        PlayerData previousPlayer = Data.PlayerData;
        string previousSavePath = Data.SavePath;
        Action previousCollectionChanged = Observer.CollectionChanged;
        Action<int> previousLevelChanged = Observer.LevelChanged;
        var temporaryAssets = new List<Object>();
        GameObject root = null;
        string temporarySavePath = Path.GetFullPath("Temp/collection_validation_" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            Observer.CollectionChanged = null;
            Observer.LevelChanged = null;
            CollectionData first = MakeCollection("test_first", 3, 39, temporaryAssets);
            CollectionData second = MakeCollection("test_second", 1, 60, temporaryAssets);
            CollectionData empty = MakeCollection("test_empty", 0, 39, temporaryAssets);
            PlayerData player = new PlayerData { CurrentLevelIndex = 38 };
            Data.PlayerData = player;

            Require(!player.IsCollectionUnlocked(first), "Collection must be locked below level 39.");
            player.CurrentLevelIndex = 39;
            Require(player.IsCollectionUnlocked(first), "Collection must unlock at level 39.");
            Require(player.GetCollectionCardState(first, first.Cards[0], 39) == CollectionCardState.NotOwned,
                "Unowned card state is incorrect.");
            Require(player.AddCollectionCard(first.Cards[0]), "Could not add card.");
            Require(player.GetCollectionCardState(first, first.Cards[0], 39) == CollectionCardState.Owned,
                "Owned card state is incorrect.");
            Require(player.AddCollectionCard(first.Cards[0]), "Could not add duplicate.");
            Require(player.GetOwnedCollectionCardCount(first) == 1, "Duplicates must not increase unique progress.");
            Require(player.GetCollectionCardState(first, first.Cards[0], 39) == CollectionCardState.Duplicate,
                "Duplicate state is incorrect.");
            Require(!player.AddCollectionCard(first.Cards[0], -1), "Negative quantities must be rejected.");
            Require(!player.IsCollectionUnlocked(second), "A higher-level collection must stay locked.");
            Require(player.UnlockCollection(second), "Explicit collection unlock failed.");

            Directory.CreateDirectory(Path.GetDirectoryName(temporarySavePath));
            Data.SavePath = temporarySavePath;
            Data.SaveData();
            Require(!File.ReadAllText(temporarySavePath).Contains(first.Cards[0].Id), "Save should be encrypted.");
            Data.PlayerData = new PlayerData();
            Data.LoadData();
            player = Data.PlayerData;
            Require(player.GetCollectionCardQuantity(first.Cards[0].Id) == 2, "Card quantity did not survive save/load.");
            Require(player.IsCollectionUnlocked(second), "Explicit unlock did not survive save/load.");
            PlayerData oldSave = JsonConvert.DeserializeObject<PlayerData>("{\"currentLevelIndex\":39}");
            Require(oldSave.CollectionCards.Count == 0 && oldSave.UnlockedCollections.Count == 0,
                "Old saves must load with empty Collection progress.");
            PlayerData nullSave = JsonConvert.DeserializeObject<PlayerData>(
                "{\"collectionCards\":null,\"unlockedCollections\":null}");
            Require(nullSave.CollectionCards.Count == 0 && nullSave.UnlockedCollections.Count == 0,
                "Null progress fields must be tolerated.");

            // Keep objects inactive to avoid gameplay, sound and entrance effects in the open scene.
            root = new GameObject("CollectionValidation") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            PopupCollectionInfor info = Object.Instantiate(infoPrefab, root.transform).GetComponent<PopupCollectionInfor>();
            info.Setup(first);
            CollectionCardItem[] initialItems = info.GetComponentsInChildren<CollectionCardItem>(true);
            Require(initialItems.Length == 3, "Details did not render all cards.");
            Require(initialItems[0].State == CollectionCardState.Duplicate, "Details did not use player progress.");
            info.Setup(second);
            CollectionCardItem[] reusedItems = info.GetComponentsInChildren<CollectionCardItem>(true);
            Require(reusedItems.Length == 3, "Card views should be reused.");
            Require(reusedItems.Count(item => item.gameObject.activeSelf) == 1, "Old card views remain visible.");
            Require(reusedItems[0] == initialItems[0] && reusedItems[0].CardData == second.Cards[0],
                "Reused card still contains the previous collection.");
            Require(reusedItems[1].CardData == null && reusedItems[2].CardData == null,
                "Hidden items must clear old card data.");
            Require(Read<TMP_Text>(info, "titleText").text == second.DisplayName, "Title was not replaced.");
            Require(Read<TMP_Text>(info, "progressText").text == "0/1", "Progress was not replaced.");
            Require(Read<ScrollRect>(info, "cardScrollRect").velocity == Vector2.zero, "Scroll motion was not reset.");
            info.Setup(empty);
            Require(Read<GameObject>(info, "emptyState").activeSelf, "Empty collection needs an empty state.");
            Require(initialItems.All(item => !item.gameObject.activeSelf), "Empty collection retained cards.");
            info.Setup(null);
            Require(info.SelectedCollection == null && Read<TMP_Text>(info, "titleText").text == string.Empty,
                "Null Setup must clear selection and title.");

            PopupCollection collection = Object.Instantiate(collectionPrefab, root.transform).GetComponent<PopupCollection>();
            player.CurrentLevelIndex = 38;
            collection.Show(PopupAnimation.None);
            Require(Read<GameObject>(collection, "lockPopup").activeSelf &&
                    !Read<GameObject>(collection, "unlockPopup").activeSelf, "Level 38 panel state is incorrect.");
            player.CurrentLevelIndex = 39;
            collection.Show(PopupAnimation.None);
            Require(!Read<GameObject>(collection, "lockPopup").activeSelf &&
                    Read<GameObject>(collection, "unlockPopup").activeSelf, "Level 39 panel state is incorrect.");
            CollectionButton button = collection.GetComponentInChildren<CollectionButton>(true);
            CollectionData clicked = null;
            int clicks = 0;
            Action<CollectionData> onClick = data => { clicked = data; clicks++; };
            button.Setup(first, onClick);
            button.Setup(second, onClick);
            button.Refresh(player, 39);
            button.GetComponent<CustomButton>().Click.Invoke();
            Require(clicked == second && clicks == 1, "Button callback is stale or registered more than once.");

            CollectionConfig expanded = Object.Instantiate(config);
            temporaryAssets.Add(expanded);
            SerializedObject expandedSerialized = new SerializedObject(expanded);
            SerializedProperty list = expandedSerialized.FindProperty("collections");
            int originalCount = list.arraySize;
            list.InsertArrayElementAtIndex(originalCount);
            list.GetArrayElementAtIndex(originalCount).objectReferenceValue = first;
            expandedSerialized.ApplyModifiedPropertiesWithoutUndo();
            SetReference(collection, "collectionConfig", expanded);
            collection.Show(PopupAnimation.None);
            Require(collection.GetComponentsInChildren<CollectionButton>(true).Length == originalCount + 1,
                "Adding a collection to the config must create a button automatically.");

            // Verify duplicate IDs are rejected instead of silently mapping to the wrong saved item.
            ((List<CollectionData>)expanded.Collections)[originalCount] = config.Collections[0];
            errors.Clear();
            Require(!expanded.ValidateData(errors), "Duplicate collection/card IDs were accepted.");
            Debug.Log("[CollectionValidation] PASS: assets, level gate, ownership, duplicates, save/load, old saves, reused UI, callbacks and catalog growth.");
        }
        finally
        {
            if (root != null)
                Object.DestroyImmediate(root);
            foreach (Object asset in temporaryAssets)
                Object.DestroyImmediate(asset);
            Data.PlayerData = previousPlayer;
            Data.SavePath = previousSavePath;
            Observer.CollectionChanged = previousCollectionChanged;
            Observer.LevelChanged = previousLevelChanged;
            if (File.Exists(temporarySavePath))
                File.Delete(temporarySavePath);
        }
    }

    private static CollectionData MakeCollection(string id, int cardCount, int level, List<Object> temporaryAssets)
    {
        CollectionData collection = ScriptableObject.CreateInstance<CollectionData>();
        collection.name = id;
        temporaryAssets.Add(collection);
        var serialized = new SerializedObject(collection);
        serialized.FindProperty("id").stringValue = id;
        serialized.FindProperty("displayName").stringValue = id;
        serialized.FindProperty("unlockLevel").intValue = level;
        SerializedProperty cards = serialized.FindProperty("cards");
        cards.arraySize = cardCount;
        for (int i = 0; i < cardCount; i++)
        {
            SerializedProperty card = cards.GetArrayElementAtIndex(i);
            card.FindPropertyRelative("id").stringValue = id + "_card_" + i;
            card.FindPropertyRelative("displayName").stringValue = "Card " + i;
            card.FindPropertyRelative("unlockLevel").intValue = 1;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return collection;
    }

    private static void CheckReferences(Object target, params string[] names)
    {
        foreach (string name in names)
            Require(Read<Object>(target, name) != null, target.name + ": missing " + name);
    }

    private static T Read<T>(Object target, string name) where T : Object
    {
        return new SerializedObject(target).FindProperty(name).objectReferenceValue as T;
    }

    private static void SetReference(Object target, string name, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("[CollectionValidation] " + message);
    }
}
