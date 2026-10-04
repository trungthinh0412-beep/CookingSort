using System;
using System.Collections;
using System.Collections.Generic;
using CustomTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PopupWin : Popup, IPointerClickHandler
{
    [SerializeField] private CustomButton btnBonusRewardAds;
    [SerializeField] private CustomButton btnClaim;
    [SerializeField] private WinArrowItem winArrowItem;
    [SerializeField] private TextMeshProUGUI totalGoldText;
    [SerializeField] private TextMeshProUGUI adsGoldText;
    [SerializeField] private List<SequenceShowObject> sequenceShowObjects;
    [SerializeField] private List<ParticleSystem> listFx;
    [SerializeField] private RectTransform groupCoin;
    [Header("Opening Animation")]
    [SerializeField] private RectTransform kingWinAnimation;
    [SerializeField] private UISpriteAnimator kingWinSpriteAnimator;
    [SerializeField, Min(0.01f)] private float kingScaleDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float goodJobDelay = 4f;
    
    [SerializeField] private Image featureProgressFill;

    private PopupInGame _popupInGame;
    private int _winGold = 10;
    private bool _continueRequested;
    private Coroutine _showGoodJobRoutine;

    protected override void OnEnable()
    {
        base.OnEnable();
        _continueRequested = false;
        _showGoodJobRoutine = null;
    }

    protected override void OnDisable()
    {
        if (_showGoodJobRoutine != null)
            StopCoroutine(_showGoodJobRoutine);

        _showGoodJobRoutine = null;
        base.OnDisable();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Only the full-screen background skips the delay. Clicking content
        // inside the win panel keeps its own button behaviour.
        Transform clickedTransform = eventData?.pointerPress?.transform;
        if (background != null && clickedTransform != background)
            return;

        ContinueToGoodJob(playClickSound: true);
    }

    private IEnumerator ShowGoodJobAfterDelay()
    {
        yield return new WaitForSecondsRealtime(goodJobDelay);
        _showGoodJobRoutine = null;
        ContinueToGoodJob(playClickSound: false);
    }

    private void ContinueToGoodJob(bool playClickSound)
    {
        if (_continueRequested)
            return;

        _continueRequested = true;

        if (_showGoodJobRoutine != null)
        {
            StopCoroutine(_showGoodJobRoutine);
            _showGoodJobRoutine = null;
        }

        if (playClickSound && SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        PopupController popupController = PopupController.Instance;

        if (popupController != null &&
            popupController.Get<PopupGoodJob>() == null)
        {
            // Covers play sessions that started before the new prefab was
            // imported into PopupConfig.
            popupController.Initialize();
        }

        if (popupController == null)
        {
            Hide(PopupAnimation.None);
            return;
        }

        popupController.Hide<PopupWin>(PopupAnimation.None);
        popupController.Show<PopupInGame>(PopupAnimation.None);

        Level currentLevel = GameManager.Instance != null &&
                             GameManager.Instance.levelController != null
            ? GameManager.Instance.levelController.currentLevel
            : null;

        if (currentLevel != null && currentLevel.gameObject.activeInHierarchy)
        {
            currentLevel.PlayWinCleanup(CompleteToGoodJob);
            return;
        }

        CompleteToGoodJob();
    }

    private void CompleteToGoodJob()
    {
        PopupController popupController = PopupController.Instance;
        if (popupController == null)
        {
            Hide(PopupAnimation.None);
            return;
        }

        if (popupController.Get<PopupGoodJob>() == null)
        {
            Debug.LogError(
                "[PopupWin] PopupGoodJob is not registered in PopupConfig."
            );
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnHome();
        }
        else
        {
            popupController.Hide<PopupInGame>(PopupAnimation.None);
            popupController.Show<PopupBackground>(PopupAnimation.None);
            popupController.Show<PopupHome>(PopupAnimation.None);
        }

        popupController.PlayHomeEntranceAnimation();
        popupController.Show<PopupGoodJob>(PopupAnimation.None);
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();

        if (kingWinAnimation != null)
        {
            kingWinAnimation.localScale = Vector3.zero;

            if (kingWinSpriteAnimator == null)
                kingWinSpriteAnimator = kingWinAnimation.GetComponent<UISpriteAnimator>();
        }

        kingWinSpriteAnimator?.RestartAnimation();

        Setup();
    }
    protected override void AfterShown()
    {
        base.AfterShown();

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PauseBackground();
            SoundController.Instance.PlayFX(SoundName.SmallWin);
        }

        SequenceShow();

        if (_showGoodJobRoutine == null)
            _showGoodJobRoutine = StartCoroutine(ShowGoodJobAfterDelay());
    }

    void Start()
    {
        _popupInGame = PopupController.Instance.Get<PopupInGame>() as PopupInGame;
    }

    void SequenceShow()
    {
        if (kingWinAnimation != null)
        {
            Tween.Scale(
                kingWinAnimation,
                Vector3.zero,
                Vector3.one,
                kingScaleDuration,
                Ease.OutBack,
                useUnscaledTime: true);
        }

        if (listFx != null)
        {
            for (int i = 0; i < listFx.Count; i++)
            {
                if (listFx[i] != null)
                {
                    listFx[i].gameObject.SetActive(true);
                    listFx[i].Play();
                }
            }
        }
        
        if (sequenceShowObjects != null)
        {
            foreach (var x in sequenceShowObjects)
            {
                if (x.gameObjects != null)
                {
                    foreach (var obj in x.gameObjects)
                    {
                        if (obj != null) obj.SetActive(false);
                    }
                }
            }
        }
        LoopShow(0);
    }
    
    void LoopShow(int indexShow)
    {
        if (sequenceShowObjects == null || indexShow >= sequenceShowObjects.Count) return;
        var groupTargetShow = sequenceShowObjects[indexShow];
       
        if (groupTargetShow.gameObjects != null)
        {
            foreach (var x in groupTargetShow.gameObjects)
            {
                if (x != null)
                {
                    x.transform.localScale = Vector3.zero;
                    x.gameObject.SetActive(true);
                    Tween.Scale(x.transform, Vector3.one, groupTargetShow.timeShow).OnComplete(() =>
                    {
                        indexShow++;
                        LoopShow(indexShow);
                    });
                }
            }
        }
    }

    public void Setup()
    {
        if (btnBonusRewardAds != null) btnBonusRewardAds.gameObject.SetActive(true);
        if (btnClaim != null) btnClaim.gameObject.SetActive(true);

        if (listFx != null)
        {
            for (int i = 0; i < listFx.Count; i++)
            {
                if (listFx[i] != null) listFx[i].gameObject.SetActive(false);
            }
        }

        if (sequenceShowObjects != null)
        {
            foreach (var x in sequenceShowObjects)
            {
                if (x.gameObjects != null)
                {
                    foreach (var obj in x.gameObjects)
                    {
                        if (obj != null) obj.SetActive(false);
                    }
                }
            }
        }

        SetupTotalGold();
    }

    private void SetupTotalGold()
    {
        // The reward widgets are temporarily absent from PopupWin.prefab.
        // Skip this optional section until those references are restored.
        //TotalGold Compelete
        if (winArrowItem == null ||
            winArrowItem.currentWinBonusArea == null ||
            totalGoldText == null ||
            adsGoldText == null)
        {
            return;
        }

        totalGoldText.text = $"{winArrowItem.currentWinBonusArea.MultiBonus * _winGold} <sprite name=\"Gold\">";
        adsGoldText.text = $"{winArrowItem.currentWinBonusArea.MultiBonus * _winGold} <sprite name=\"Gold\">";
    }
    
    public void OnClickClaimAds()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        SoundController.Instance.PlayFX(SoundName.ClaimReward);
        AdsController.Instance.ShowRewardAds(() =>
        {
            ClaimReward(true);
        }, placement: "PopupWin_OnClickClaimAds");
    }

    public void OnClickClaim()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        SoundController.Instance.PlayFX(SoundName.ClaimReward);
        AdsController.Instance.ShowInterstitial(() =>
        {
            ClaimReward(false);
        }, placement: "PopupWin_OnClickClaim");
    }

    private void ClaimReward(bool isWatchAds)
    {
        if (isWatchAds)
        {
            Data.PlayerData.SavingReward = new RewardData(_winGold * winArrowItem.currentWinBonusArea.MultiBonus, _popupInGame.CurrentStar);
        }
        else
        {
            Data.PlayerData.SavingReward = new RewardData(_winGold, _popupInGame.CurrentStar);
        }
        btnBonusRewardAds.gameObject.SetActive(false);
        btnClaim.gameObject.SetActive(false);
        
        GameManager.Instance.ReturnHome();
    }
}
[Serializable]
public struct SequenceShowObject
{
    public List<GameObject> gameObjects;
    public float timeShow;
}
