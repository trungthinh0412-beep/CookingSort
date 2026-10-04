using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CardSlotHolder))]
public class CardSlotHolderHighValue :
    MonoBehaviour,
    ICardSlotHolderRule
{
    [Header("High Value Config")]
    [SerializeField, Range(1, 20)]
    private int minimumCardValue = 4;

    [Header("Badge Visual")]
    [SerializeField] private GameObject badgeVisual;
    [SerializeField] private TMP_Text minimumValueText;

    public int MinimumCardValue => minimumCardValue;

    private void Awake()
    {
        UpdateVisual();
    }

    private void OnEnable()
    {
        UpdateVisual();
    }

    public bool CanReceiveCard(CardType cardType)
    {
        int cardValue = (int)cardType + 1;
        return cardValue >= minimumCardValue;
    }

    public void OnHolderCardsChanged()
    {
    }

    private void UpdateVisual()
    {
        if (badgeVisual != null)
            badgeVisual.SetActive(true);

        if (minimumValueText != null)
            minimumValueText.text = minimumCardValue.ToString();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minimumCardValue =
            Mathf.Clamp(minimumCardValue, 1, 20);
        UpdateVisual();
    }
#endif
}
