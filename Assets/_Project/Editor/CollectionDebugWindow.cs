using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class CollectionDebugWindow : EditorWindow
{
    private CollectionData _collection;
    private int _cardIndex;
    private int _amount = 1;
    private string _message;

    [MenuItem("Tools/Collection/Debug Cards")]
    private static void Open() => GetWindow<CollectionDebugWindow>("Collection Debug");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Use during Play Mode. These actions update the current player's real save.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !CollectionManager.IsInitialized))
        {
            int unlockLevel = CollectionManager.Config != null ? CollectionManager.Config.UnlockLevel : 39;
            EditorGUILayout.HelpBox($"Unlock and Open raises the saved player level to at least {unlockLevel}.", MessageType.Info);
            if (GUILayout.Button("Unlock and Open Collection"))
            {
                Data.PlayerData.CurrentLevelIndex = Mathf.Max(Data.PlayerData.CurrentLevelIndex, unlockLevel);
                Data.SaveData();
                Observer.CollectionChanged?.Invoke();
                PopupController controller = PopupController.Instance;
                if (controller != null && controller.Get<PopupCollection>() is PopupCollection)
                {
                    controller.Show<PopupCollection>(PopupAnimation.ScaleFade);
                    _message = $"Collection unlocked. Player level: {Data.PlayerData.CurrentLevelIndex}.";
                }
                else
                {
                    _message = "Level saved, but PopupCollection is unavailable in the current scene.";
                }
            }
        }
        _collection = (CollectionData)EditorGUILayout.ObjectField("Collection", _collection, typeof(CollectionData), false);
        if (_collection == null && CollectionManager.Config != null && CollectionManager.Config.Collections.Count > 0)
            _collection = CollectionManager.Config.Collections[0];

        using (new EditorGUI.DisabledScope(!Application.isPlaying || !CollectionManager.IsInitialized || _collection == null))
        {
            if (_collection != null)
            {
                EditorGUILayout.LabelField("Progress", $"{CollectionManager.GetOwnedUniqueCardCount(_collection.Id)}/{_collection.TotalCards}");
                EditorGUILayout.LabelField("Reward", CollectionManager.GetRewardState(_collection.Id).ToString());
                var names = new List<string>();
                foreach (CollectionCardData card in _collection.Cards)
                    names.Add(card == null ? "Missing card" : card.DisplayName + " / " + card.Id);
                if (names.Count > 0)
                    _cardIndex = EditorGUILayout.Popup("Card", Mathf.Clamp(_cardIndex, 0, names.Count - 1), names.ToArray());
                _amount = Mathf.Max(1, EditorGUILayout.IntField("Amount", _amount));
            }
            if (GUILayout.Button("Add specific Card"))
                Add(_collection.TotalCards > 0 ? _collection.Cards[_cardIndex] : null, _amount);
            if (GUILayout.Button("Add random unowned Card"))
            {
                var unowned = new List<CollectionCardData>();
                foreach (CollectionCardData card in _collection.Cards)
                    if (card != null && CollectionManager.GetCardProgress(card.Id).Quantity == 0)
                        unowned.Add(card);
                Add(unowned.Count == 0 ? null : unowned[Random.Range(0, unowned.Count)], 1);
            }
            if (GUILayout.Button("Add duplicate Card"))
            {
                CollectionCardData owned = null;
                foreach (CollectionCardData card in _collection.Cards)
                    if (card != null && CollectionManager.GetCardProgress(card.Id).Quantity > 0) { owned = card; break; }
                Add(owned, _amount);
            }
            if (GUILayout.Button("Complete selected Collection"))
            {
                foreach (CollectionCardData card in _collection.Cards)
                    if (card != null && CollectionManager.GetCardProgress(card.Id).Quantity == 0)
                        Add(card, 1);
            }
            if (GUILayout.Button("Reset selected Collection"))
            {
                CollectionManager.DebugResetCollection(_collection.Id);
                _message = "Cards, NEW flags and claimed state reset. Previously granted currency is unchanged.";
            }
            if (GUILayout.Button("Mark reward unclaimed"))
            {
                CollectionManager.DebugResetCollection(_collection.Id, true);
                _message = "Reward marked unclaimed for testing.";
            }
            if (GUILayout.Button("Claim reward"))
                _message = CollectionManager.TryClaimReward(_collection.Id) ? "Reward claimed." : "Claim rejected.";
            if (GUILayout.Button("Open selected Collection"))
                (PopupController.Instance?.Get<PopupCollection>() as PopupCollection)?.OpenCollection(_collection);
        }
        if (!string.IsNullOrEmpty(_message))
            EditorGUILayout.HelpBox(_message, MessageType.None);
    }

    private void Add(CollectionCardData card, int amount)
    {
        if (card == null)
        {
            _message = "No matching card.";
            return;
        }
        CardAddResult result = CollectionManager.AddCard(card.Id, amount);
        _message = result.Success
            ? $"{result.CardId}: {result.OldQuantity} -> {result.NewQuantity}; new={result.IsNewCard}, duplicate={result.IsDuplicate}, completedNow={result.CollectionCompletedNow}"
            : "Grant rejected: " + result.Status;
        Repaint();
    }
}
