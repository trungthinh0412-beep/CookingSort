using System.Collections.Generic;
using CustomTween;
using UnityEngine;
using UnityEngine.UI;

public class PopupClaimStarChest : Popup
{
    [SerializeField] private GameObject btnTapToOpen;
    [SerializeField] private GameObject btnTapToContinue;
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip clipOpen;
    [SerializeField] private AnimationClip clipIdle;
    [Header("Config Open")]
    [SerializeField] private RectTransform bomb;
    [SerializeField] private RectTransform shuffer;
    [SerializeField] private RectTransform posToBomb;
    [SerializeField] private RectTransform posToShuffer;
    [SerializeField] private float timeFly = 1;
    [SerializeField] private float scaleBooster = 2;
    [SerializeField] private Image chestImg;
    [SerializeField] private Sprite chestIdleSprite;
    [SerializeField] private Ease easeFly;

    List<Tween> _tweens = new();

    protected override void BeforeShow()
    {
        base.BeforeShow();
        ResetIdle();
        btnTapToOpen.SetActive(true);
        btnTapToContinue.SetActive(false);
    }
    void ResetIdle()
    {
        bomb.gameObject.SetActive(false);
        shuffer.gameObject.SetActive(false);
        bomb.anchoredPosition = chestImg.rectTransform.anchoredPosition;
        shuffer.anchoredPosition = chestImg.rectTransform.anchoredPosition;
        chestImg.sprite = chestIdleSprite;
    }
    protected override void AfterShown()
    {
        base.AfterShown();
        animator.Play(clipIdle.name);
    }
    public void OnClickOpen()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        btnTapToOpen.SetActive(false);
        animator.Play(clipOpen.name);
        var duration = clipOpen.length;
        _tweens.Add(Tween.Delay(duration, () =>
        {
            bomb.gameObject.SetActive(true);
            shuffer.gameObject.SetActive(true);
            bomb.localScale = Vector3.zero;
            shuffer.localScale = Vector3.zero;
            // fly booster and scale up
            _tweens.Add(Tween.Scale(bomb, Vector3.one * scaleBooster, timeFly));
            _tweens.Add(Tween.Scale(shuffer, Vector3.one * scaleBooster, timeFly));
            _tweens.Add(Tween.UIAnchoredPosition(bomb, posToBomb.anchoredPosition, timeFly, easeFly));
            _tweens.Add(Tween.UIAnchoredPosition(shuffer, posToShuffer.anchoredPosition, timeFly, easeFly).OnComplete(() =>
            {
                btnTapToContinue.SetActive(true);
            }));
        }));
    }
    public void Close()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        foreach (var x in _tweens)
        {
            x.Stop();
        }
        Hide();
    }
}


