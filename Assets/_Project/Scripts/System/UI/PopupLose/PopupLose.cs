using System.Collections;
using System.Collections.Generic;
using CustomTween;
using UnityEngine;

public class PopupLose : Popup
{
    [Header("Copied PopupWin Presentation")]
    [SerializeField] private List<SequenceShowObject> sequenceShowObjects;
    [SerializeField] private List<ParticleSystem> listFx;
    [SerializeField] private RectTransform kingWinAnimation;
    [SerializeField] private UISpriteAnimator kingWinSpriteAnimator;
    [SerializeField, Min(0.01f)] private float kingScaleDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float goodJobDelay = 4f;

    private Coroutine _closeRoutine;
    private bool _closeRequested;

    protected override void OnEnable()
    {
        base.OnEnable();
        _closeRequested = false;
        _closeRoutine = null;
    }

    protected override void OnDisable()
    {
        if (_closeRoutine != null)
            StopCoroutine(_closeRoutine);

        _closeRoutine = null;
        base.OnDisable();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();

        if (kingWinAnimation != null)
        {
            kingWinAnimation.localScale = Vector3.zero;
            if (kingWinSpriteAnimator == null)
            {
                kingWinSpriteAnimator =
                    kingWinAnimation.GetComponent<UISpriteAnimator>();
            }
        }

        kingWinSpriteAnimator?.RestartAnimation();
        SetupPresentation();
    }

    protected override void AfterShown()
    {
        base.AfterShown();
        PlayPresentation();
        _closeRoutine = StartCoroutine(CloseAfterPresentation());
    }

    private IEnumerator CloseAfterPresentation()
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0.01f, goodJobDelay)
        );
        _closeRoutine = null;
        ContinueToBoosterQuit(playClickSound: false);
    }

    private void SetupPresentation()
    {
        if (listFx != null)
        {
            for (int i = 0; i < listFx.Count; i++)
            {
                if (listFx[i] != null)
                    listFx[i].gameObject.SetActive(false);
            }
        }

        if (sequenceShowObjects == null)
            return;

        for (int i = 0; i < sequenceShowObjects.Count; i++)
        {
            List<GameObject> objects = sequenceShowObjects[i].gameObjects;
            if (objects == null)
                continue;

            for (int j = 0; j < objects.Count; j++)
            {
                if (objects[j] != null)
                    objects[j].SetActive(false);
            }
        }
    }

    private void PlayPresentation()
    {
        if (kingWinAnimation != null)
        {
            Tween.Scale(
                kingWinAnimation,
                Vector3.zero,
                Vector3.one,
                kingScaleDuration,
                Ease.OutBack,
                useUnscaledTime: true
            );
        }

        if (listFx != null)
        {
            for (int i = 0; i < listFx.Count; i++)
            {
                if (listFx[i] == null)
                    continue;

                listFx[i].gameObject.SetActive(true);
                listFx[i].Play();
            }
        }

        ShowSequence(0);
    }

    private void ShowSequence(int index)
    {
        if (sequenceShowObjects == null ||
            index >= sequenceShowObjects.Count)
        {
            return;
        }

        SequenceShowObject group = sequenceShowObjects[index];
        if (group.gameObjects == null || group.gameObjects.Count == 0)
        {
            ShowSequence(index + 1);
            return;
        }

        bool hasVisibleObject = false;
        bool hasAdvanced = false;
        for (int i = 0; i < group.gameObjects.Count; i++)
        {
            GameObject item = group.gameObjects[i];
            if (item == null)
                continue;

            hasVisibleObject = true;
            item.transform.localScale = Vector3.zero;
            item.SetActive(true);
            Tween.Scale(item.transform, Vector3.one, group.timeShow)
                .OnComplete(() =>
                {
                    if (hasAdvanced)
                        return;

                    hasAdvanced = true;
                    ShowSequence(index + 1);
                });
        }

        if (!hasVisibleObject)
            ShowSequence(index + 1);
    }

    public void OnClickClaim()
    {
        ContinueToBoosterQuit(playClickSound: true);
    }

    public void OnClickBack()
    {
        ContinueToBoosterQuit(playClickSound: true);
    }

    public void OnClickResume()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        Hide();
        PopupController.Instance.Show<PopupContinue>();
    }

    private void ContinueToBoosterQuit(bool playClickSound)
    {
        if (_closeRequested)
            return;

        _closeRequested = true;
        if (playClickSound && SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReplayGamePausedWithPopupBoosterQuit();
            return;
        }

        PopupController.Instance?.Hide<PopupLose>();
        PopupController.Instance?.Show<PopupBoosterQuit>();
    }
}
