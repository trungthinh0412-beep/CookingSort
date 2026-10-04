using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CardSlotHolder))]
public class CardSlotHolderWild :
    MonoBehaviour,
    ICardSlotHolderRule
{
    [Header("Wild Visual")]
    [SerializeField] private SpriteRenderer trayRenderer;
    [SerializeField] private Color idleColor =
        new Color32(130, 130, 130, 255);
    [SerializeField] private GameObject idleVisual;
    [SerializeField] private GameObject assignedVisual;

    private CardSlotHolder _holder;
    private CardType? _assignedCardType;

    public CardType? AssignedCardType => _assignedCardType;

    private void Awake()
    {
        _holder = GetComponent<CardSlotHolder>();
        RefreshState();
    }

    private void OnEnable()
    {
        if (_holder == null)
            _holder = GetComponent<CardSlotHolder>();

        RefreshState();
    }

    public bool CanReceiveCard(CardType cardType)
    {
        SyncAssignedType();

        return !_assignedCardType.HasValue ||
               _assignedCardType.Value == cardType;
    }

    public void OnHolderCardsChanged()
    {
        RefreshState();
    }

    private void RefreshState()
    {
        SyncAssignedType();

        bool assigned = _assignedCardType.HasValue;

        if (trayRenderer != null)
        {
            trayRenderer.color = assigned
                ? Card.GetThemeColor(_assignedCardType.Value)
                : idleColor;
        }

        if (idleVisual != null)
            idleVisual.SetActive(!assigned);

        if (assignedVisual != null)
            assignedVisual.SetActive(assigned);
    }

    private void SyncAssignedType()
    {
        _assignedCardType =
            _holder != null
                ? _holder.GetTopCardType()
                : null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && trayRenderer != null)
            trayRenderer.color = idleColor;
    }
#endif
}
