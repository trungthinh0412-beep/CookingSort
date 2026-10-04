using UnityEngine;

public class CardSlot : MonoBehaviour
{
    [SerializeField] private Card _card;

    public Card Card
    {
        get => _card;
        set => _card = value;
    }

    public bool IsEmpty => _card == null;

    public CardType? GetCardType()
    {
        return _card != null ? _card.CardType : null;
    }

    public void SetCard(Card card)
    {
        _card = card;

        if (_card != null)
        {
            _card.transform.SetParent(transform);
            _card.transform.localPosition = Vector3.zero;
        }
    }

    public Card RemoveCard()
    {
        Card card = _card;
        _card = null;
        return card;
    }
}
