using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CustomButton))]
public sealed class CollectionButton : MonoBehaviour
{
    [SerializeField] private CollectionData collectionData;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image coverImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Image progressFill;
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private TMP_Text unlockLevelText;
    [SerializeField] private GameObject completeRoot;
    [SerializeField] private TMP_Text rewardStateText;

    private CustomButton _button;
    private Action<CollectionData> _onSelected;

    public CollectionData CollectionData => collectionData;

    public void Setup(CollectionData data, Action<CollectionData> onSelected)
    {
        collectionData = data;
        _onSelected = onSelected;
        BindClick();

        if (backgroundImage != null)
            backgroundImage.sprite = data == null ? null : data.ButtonSprite;
        if (coverImage != null)
        {
            coverImage.sprite = data == null ? null : data.CoverSprite;
            coverImage.enabled = coverImage.sprite != null;
        }
        if (nameText != null)
            nameText.text = data == null ? string.Empty : data.DisplayName;
    }

    public void Refresh(PlayerData player, int featureUnlockLevel)
    {
        BindClick();
        bool unlocked = collectionData != null && CollectionManager.IsCollectionUnlocked(collectionData.Id);
        _button.Interactable = unlocked;

        int owned = player == null ? 0 : player.GetOwnedCollectionCardCount(collectionData);
        int total = collectionData == null ? 0 : collectionData.TotalCards;
        if (progressText != null)
            progressText.text = $"{owned}/{total}";
        if (progressFill != null)
            progressFill.fillAmount = total == 0 ? 0f : (float)owned / total;
        if (lockedRoot != null)
            lockedRoot.SetActive(!unlocked);
        if (unlockLevelText != null)
        {
            int level = collectionData == null ? featureUnlockLevel :
                Mathf.Max(featureUnlockLevel, collectionData.UnlockLevel);
            unlockLevelText.text = $"Level {level}";
        }
        bool complete = collectionData != null && CollectionManager.IsCollectionComplete(collectionData.Id);
        if (completeRoot != null)
            completeRoot.SetActive(unlocked && complete);
        if (rewardStateText != null)
        {
            CollectionRewardState state = collectionData == null ? CollectionRewardState.Locked :
                CollectionManager.GetRewardState(collectionData.Id);
            rewardStateText.text = !unlocked || !complete ? string.Empty :
                state == CollectionRewardState.Claimed ? "Claimed" :
                state == CollectionRewardState.ReadyToClaim ? "Reward ready" : "Complete";
        }
    }

    private void BindClick()
    {
        if (_button == null)
            _button = GetComponent<CustomButton>();
        _button.Click.RemoveListener(OnClick);
        _button.Click.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (collectionData != null && _button.Interactable)
            _onSelected?.Invoke(collectionData);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.Click.RemoveListener(OnClick);
        _onSelected = null;
    }
}
