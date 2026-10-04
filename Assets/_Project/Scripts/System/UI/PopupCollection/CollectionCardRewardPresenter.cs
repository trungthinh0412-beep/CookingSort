using System.Collections;
using CustomTween;
using UnityEngine;

/// <summary>Optional receive/reveal/flight effect. It consumes an already saved result and never grants data.</summary>
public sealed class CollectionCardRewardPresenter : MonoBehaviour
{
    [SerializeField] private CollectionCardItem cardPrefab;
    [SerializeField] private RectTransform presentationRoot;
    [SerializeField] private RectTransform receiveAnchor;
    [SerializeField] private RectTransform fallbackCollectionTarget;
    [Min(0)] [SerializeField] private float holdDuration = 0.15f;
    [Min(0)] [SerializeField] private float flyDuration = 0.35f;
    [Range(0.01f, 1f)] [SerializeField] private float endScale = 0.15f;

    private CollectionCardItem _card;
    private Tween _moveTween;
    private Tween _scaleTween;

    public IEnumerator Present(CardAddResult result, RectTransform target = null)
    {
        Cancel();
        if (!result.Success || cardPrefab == null || presentationRoot == null ||
            !Application.isPlaying || !isActiveAndEnabled)
            yield break;
        if (_card == null)
            _card = Instantiate(cardPrefab, presentationRoot);
        _card.transform.SetAsLastSibling();
        _card.gameObject.SetActive(true);
        _card.transform.localScale = Vector3.one;
        _card.RectTransform.anchoredPosition = receiveAnchor == null ? Vector2.zero : LocalPoint(receiveAnchor);
        yield return _card.PlayReceive(result);
        if (holdDuration > 0)
            yield return new WaitForSecondsRealtime(holdDuration);

        if (target == null)
            target = FindTarget(result);
        if (target != null && target.gameObject.activeInHierarchy)
        {
            _moveTween = Tween.UIAnchoredPosition(_card.RectTransform, LocalPoint(target), flyDuration,
                Ease.InOutQuad, useUnscaledTime: true);
            _scaleTween = Tween.Scale(_card.transform, endScale, flyDuration, useUnscaledTime: true);
            yield return _moveTween.ToYieldInstruction();
        }
        Cancel();
    }

    private RectTransform FindTarget(CardAddResult result)
    {
        PopupController controller = PopupController.Instance;
        if (controller != null)
        {
            if (controller.Get<PopupCollectionInfor>() is PopupCollectionInfor info &&
                info.TryGetCardTarget(result.CardId, out RectTransform slot))
                return slot;
            if (controller.Get<PopupCollection>() is PopupCollection collection &&
                collection.TryGetCollectionTarget(result.CollectionId, out RectTransform button))
                return button;
        }
        return fallbackCollectionTarget;
    }

    private Vector2 LocalPoint(RectTransform target)
    {
        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Canvas rootCanvas = presentationRoot.GetComponentInParent<Canvas>();
        Camera targetCamera = targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : targetCanvas.worldCamera;
        Camera rootCamera = rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : rootCanvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, target.TransformPoint(target.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(presentationRoot, screen, rootCamera, out Vector2 point);
        return point;
    }

    public void Cancel()
    {
        if (_moveTween.isAlive)
            _moveTween.Stop();
        if (_scaleTween.isAlive)
            _scaleTween.Stop();
        if (_card != null)
        {
            _card.CancelPresentation();
            _card.gameObject.SetActive(false);
        }
    }

    private void OnDisable() => Cancel();

    private void OnDestroy()
    {
        if (_card != null)
            Destroy(_card.gameObject);
    }
}
