using CustomTween;
using Lean.Pool;
using TMPro;
using UnityEngine;

public class NotifyTagItem : MonoBehaviour
{
    [SerializeField] private float offsetY = 200;
    [SerializeField] private TextMeshProUGUI notifyText;
    [SerializeField] private CanvasGroup canvasGroup;
    
    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void SetText(string content)
    {
        notifyText.text = $"{content}";
    }

    public void Action(Vector3 position)
    {
        _rectTransform.position = position;
        var startPos = _rectTransform.localPosition;
        startPos.y = 0 - offsetY;
        startPos.z = 0;
        _rectTransform.localPosition = startPos;

        canvasGroup.alpha = 1;
        var endPos = startPos;
        endPos.y = 0;
        Tween.LocalPosition(_rectTransform, startPos, endPos, 2f, Ease.OutCubic,useUnscaledTime: true);
        Tween.Scale(_rectTransform, Vector3.one * .6f, Vector3.one, .2f, useUnscaledTime:true);
        Tween.Alpha(canvasGroup, 0, 1.5f, startDelay: 1f,useUnscaledTime: true).OnComplete(()=> LeanPool.Despawn(gameObject));
    }
}