using System.Collections;
using CustomTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CollectionCardItem : MonoBehaviour
{
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private GameObject notOwnedRoot;
    [SerializeField] private GameObject ownedRoot;
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private GameObject duplicateRoot;
    [SerializeField] private TMP_Text duplicateCountText;
    [SerializeField] private GameObject newBadge;
    [SerializeField] private GameObject revealGlow;
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private Color notOwnedColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
    [SerializeField] private Color lockedColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

    [Header("Receive Animation")]
    [Min(0)] [SerializeField] private float receiveDelay = 0.08f;
    [Min(0)] [SerializeField] private float flipDuration = 0.12f;
    [Min(0)] [SerializeField] private float popDuration = 0.18f;
    [Min(0)] [SerializeField] private float glowDuration = 0.2f;
    [Min(1)] [SerializeField] private float popScale = 1.08f;

    private Tween _scaleTween;
    private Vector3 _restScale = Vector3.one;
    private bool _cachedScale;

    public CollectionCardData CardData { get; private set; }
    public CollectionCardState State { get; private set; }
    public CollectionCardProgress Progress { get; private set; }
    public RectTransform RectTransform => (RectTransform)transform;

    public void Setup(CollectionCardData card, CollectionCardProgress progress)
    {
        CancelPresentation();
        Render(card, progress);
    }

    public void Setup(CollectionCardData card, CollectionCardState state, int quantity)
    {
        Setup(card, new CollectionCardProgress(quantity, false, state != CollectionCardState.Locked));
    }

    private void Render(CollectionCardData card, CollectionCardProgress progress)
    {
        CardData = card;
        Progress = progress;
        CollectionCardState state = progress.State;
        int quantity = progress.Quantity;
        State = state;
        bool hasCard = card != null;
        bool owned = hasCard && (state == CollectionCardState.Owned || state == CollectionCardState.Duplicate);

        if (stateText != null)
            stateText.text = !hasCard ? string.Empty :
                state == CollectionCardState.Locked ? "Locked" : owned ? "Owned" : "Not owned";
        if (notOwnedRoot != null)
            notOwnedRoot.SetActive(hasCard && state == CollectionCardState.NotOwned);
        if (ownedRoot != null)
            ownedRoot.SetActive(owned);
        if (lockedRoot != null)
            lockedRoot.SetActive(hasCard && state == CollectionCardState.Locked);
        if (duplicateRoot != null)
            duplicateRoot.SetActive(hasCard && state == CollectionCardState.Duplicate);
        if (duplicateCountText != null)
            duplicateCountText.text = hasCard && state == CollectionCardState.Duplicate
                ? $"+{Mathf.Max(0, quantity - 1)}" : string.Empty;
        if (newBadge != null)
            newBadge.SetActive(owned && progress.IsNew);
    }

    public IEnumerator PlayReceive(CardAddResult result)
    {
        if (!result.Success)
            yield break;
        Setup(result.Card, result.Before);
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            Setup(result.Card, result.After);
            yield break;
        }
        if (receiveDelay > 0)
            yield return new WaitForSecondsRealtime(receiveDelay);
        _scaleTween = Tween.Scale(transform, new Vector3(0, _restScale.y, _restScale.z), flipDuration,
            useUnscaledTime: true);
        yield return _scaleTween.ToYieldInstruction();
        Render(result.Card, result.After);
        if (revealGlow != null)
            revealGlow.SetActive(result.IsNewCard);
        _scaleTween = Tween.Scale(transform, _restScale * popScale, popDuration, Ease.OutBack, useUnscaledTime: true);
        yield return _scaleTween.ToYieldInstruction();
        _scaleTween = Tween.Scale(transform, _restScale, popDuration, useUnscaledTime: true);
        yield return _scaleTween.ToYieldInstruction();
        if (glowDuration > 0)
            yield return new WaitForSecondsRealtime(glowDuration);
        if (revealGlow != null)
            revealGlow.SetActive(false);
    }

    public void CancelPresentation()
    {
        if (!_cachedScale)
        {
            _restScale = transform.localScale;
            _cachedScale = true;
        }
        if (_scaleTween.isAlive)
            _scaleTween.Stop();
        transform.localScale = _restScale;
        if (revealGlow != null)
            revealGlow.SetActive(false);
    }

    private void OnDisable()
    {
        CancelPresentation();
    }

    public void Clear()
    {
        Setup(null, new CollectionCardProgress(0, false));
        gameObject.SetActive(false);
    }
}
