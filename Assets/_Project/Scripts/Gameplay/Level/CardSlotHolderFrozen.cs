using TMPro;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(CardSlotHolder))]
public class CardSlotHolderFrozen : MonoBehaviour
{
    [Header("Unlock Config")]
    [SerializeField, Min(0)]
    private int requiredSuccessfulStacks = 2;

    [Header("Frozen Visual")]
    [SerializeField] private GameObject frozenVisual;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private UnityEvent onUnlocked;

    [Header("Runtime")]
    [SerializeField] private int remainingSuccessfulStacks;

    private CardSlotHolder _holder;
    private Level _level;
    private bool _isSubscribed;
    private bool _initialized;

    public int RemainingSuccessfulStacks => remainingSuccessfulStacks;
    public bool IsUnlocked => remainingSuccessfulStacks <= 0;

    private void Awake()
    {
        _holder = GetComponent<CardSlotHolder>();
        _level = GetComponentInParent<Level>();
        InitializeState();
    }

    private void OnEnable()
    {
        InitializeState();
        TrySubscribe();
    }

    private void Start()
    {
        InitializeState();
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void InitializeState()
    {
        if (!_initialized)
        {
            remainingSuccessfulStacks =
                Mathf.Max(0, requiredSuccessfulStacks);
            _initialized = true;
        }

        ApplyState(false);
    }

    private void TrySubscribe()
    {
        if (_isSubscribed || IsUnlocked)
            return;

        if (_level == null)
            _level = GetComponentInParent<Level>();

        if (_level == null)
            return;

        _level.OnStackCompleted += OnStackCompleted;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _level == null)
            return;

        _level.OnStackCompleted -= OnStackCompleted;
        _isSubscribed = false;
    }

    private void OnStackCompleted(
        CardSlotHolder completedHolder,
        int mergedCardCount)
    {
        if (IsUnlocked ||
            mergedCardCount != CardSlotHolder.MAX_SLOTS)
        {
            return;
        }

        remainingSuccessfulStacks =
            Mathf.Max(0, remainingSuccessfulStacks - 1);

        bool justUnlocked = IsUnlocked;
        ApplyState(justUnlocked);

        if (justUnlocked)
            Unsubscribe();
    }

    private void ApplyState(bool invokeUnlockEvent)
    {
        bool locked = !IsUnlocked;

        if (_holder != null)
        {
            _holder.SetLocked(locked);
            _holder.NotifyCardsChanged();
        }

        if (frozenVisual != null)
            frozenVisual.SetActive(locked);

        if (countdownText != null)
        {
            countdownText.text =
                remainingSuccessfulStacks.ToString();
            countdownText.gameObject.SetActive(locked);
        }

        if (!invokeUnlockEvent)
            return;

        onUnlocked?.Invoke();

        if (_level != null)
            _level.OnBoardChanged?.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        requiredSuccessfulStacks =
            Mathf.Max(0, requiredSuccessfulStacks);

        if (!Application.isPlaying && countdownText != null)
        {
            countdownText.text =
                requiredSuccessfulStacks.ToString();
        }
    }
#endif
}
