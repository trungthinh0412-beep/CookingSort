using Lean.Pool;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CardSlotHolder))]
public class CardSlotHolderBonus : MonoBehaviour
{
    [Header("Reward Ads Unlock")]
    [SerializeField]
    private bool unlockOnlyByRewardAds = true;

    [Header("Bonus Draws")]
    [SerializeField, Min(0)]
    private int remainingDraws = 3;

    [Header("Bonus Number")]
    [SerializeField]
    private TMP_Text numberText;

    private CardSlotHolder _holder;
    private Level _level;
    private bool _isSubscribed;

    public int RemainingDraws => remainingDraws;
    public bool IsUnlocked =>
        _holder != null && !_holder.IsLocked;

    private void Awake()
    {
        _holder = GetComponent<CardSlotHolder>();
        _level = GetComponentInParent<Level>();

        // The existing CardSlotHolder prefab already contains a TMP label
        // in its locked visual. It can be assigned explicitly, or found
        // automatically when this component is added to the bonus prefab.
        if (numberText == null)
        {
            numberText = GetComponentInChildren<TMP_Text>(true);
        }

        SetBonusLocked(remainingDraws > 0);
        UpdateNumberText();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        // Start is a safe fallback if the Level reference was not available
        // yet during OnEnable because of Unity script execution order.
        SetBonusLocked(remainingDraws > 0);
        UpdateNumberText();
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        if (_level == null)
        {
            _level = GetComponentInParent<Level>();
        }

        if (_level == null)
        {
            return;
        }

        _level.OnDealCountChanged += OnDeckDrawn;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _level == null)
        {
            return;
        }

        _level.OnDealCountChanged -= OnDeckDrawn;
        _isSubscribed = false;
    }

    private void OnDeckDrawn(int unusedDealCount)
    {
        if (unlockOnlyByRewardAds)
            return;

        if (remainingDraws <= 0)
        {
            return;
        }

        remainingDraws--;
        UpdateNumberText();

        if (remainingDraws == 0)
        {
            SetBonusLocked(false);

            if (numberText != null)
            {
                numberText.gameObject.SetActive(false);
            }
        }
    }

    public bool UnlockByRewardAds()
    {
        if (_holder == null)
            _holder = GetComponent<CardSlotHolder>();

        if (_holder == null)
            return false;

        if (IsUnlocked)
            return true;

        remainingDraws = 0;
        SetBonusLocked(false);
        UpdateNumberText();

        if (_level == null)
            _level = GetComponentInParent<Level>();

        if (_level != null)
            _level.OnBoardChanged?.Invoke();

        return true;
    }

    private void SetBonusLocked(bool locked)
    {
        if (_holder != null)
        {
            _holder.SetLocked(locked);
        }
    }

    private void UpdateNumberText()
    {
        if (numberText != null)
        {
            numberText.text = remainingDraws.ToString();
            numberText.gameObject.SetActive(remainingDraws > 0);
        }
    }
    
}
