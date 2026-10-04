using System;
using System.Collections.Generic;
using CustomTween;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PopupWin : Popup
{
    [SerializeField] private CustomButton btnBonusRewardAds;
    [SerializeField] private CustomButton btnClaim;
    [SerializeField] private WinArrowItem winArrowItem;
    [SerializeField] private TextMeshProUGUI totalGoldText;
    [SerializeField] private TextMeshProUGUI adsGoldText;
    [SerializeField] private List<SequenceShowObject> sequenceShowObjects;
    [SerializeField] private List<ParticleSystem> listFx;
    [SerializeField] private RectTransform groupCoin;
    [SerializeField] private float distanceCoinMoveUp = 15;
    
    [SerializeField] private Image featureProgressFill;

    private PopupInGame _popupInGame;
    private int _winGold = 10;

    protected override void BeforeShow()
    {
        base.BeforeShow();
        Setup();
    }
    protected override void AfterShown()
    {
        base.AfterShown();
        SequenceShow();
    }

    void Start()
    {
        _popupInGame = PopupController.Instance.Get<PopupInGame>() as PopupInGame;
    }

    private void Update()
    {
        SetupTotalGold();
    }
    void SequenceShow()
    {
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
        _winGold = GameManager.Instance.CanClaimLevelReward ? 10 : 0;
        SoundController.Instance.PlayFX(SoundName.ShowWinPopup);

        if (btnBonusRewardAds != null) btnBonusRewardAds.gameObject.SetActive(_winGold > 0);
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
        if (winArrowItem.currentWinBonusArea == null) return;
        totalGoldText.text = $"{winArrowItem.currentWinBonusArea.MultiBonus * _winGold} <sprite name=\"Gold\">";
        adsGoldText.text = $"{winArrowItem.currentWinBonusArea.MultiBonus * _winGold} <sprite name=\"Gold\">";
    }
    
    public void OnClickClaimAds()
    {
        if (_winGold == 0) { ClaimReward(false); return; }
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        SoundController.Instance.PlayFX(SoundName.ClaimReward);
        AdsController.Instance.ShowRewardAds(() =>
        {
            ClaimReward(true);
        }, placement: "PopupWin_OnClickClaimAds");
    }

    public void OnClickClaim()
    {
        if (_winGold == 0) { ClaimReward(false); return; }
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        SoundController.Instance.PlayFX(SoundName.ClaimReward);
        AdsController.Instance.ShowInterstitial(() =>
        {
            ClaimReward(false);
        }, placement: "PopupWin_OnClickClaim");
    }

    private void ClaimReward(bool isWatchAds)
    {
        if (_winGold == 0)
        {
            ReturnHomeAndShowCompletion();
            return;
        }
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
        
        ReturnHomeAndShowCompletion();
    }

    private void ReturnHomeAndShowCompletion()
    {
        GameManager.Instance.ReturnHome();

        if (PopupController.Instance.Get<PopupGoodJob>() != null)
            PopupController.Instance.Show<PopupGoodJob>(PopupAnimation.ScaleFade);
    }
}
[Serializable]
public struct SequenceShowObject
{
    public List<GameObject> gameObjects;
    public float timeShow;
}
