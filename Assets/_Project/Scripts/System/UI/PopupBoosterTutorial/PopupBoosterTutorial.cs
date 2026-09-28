using System;
using UnityEngine;

public class PopupBoosterTutorial : Popup
{
    [SerializeField] private GameObject boosterBomb;
    [SerializeField] private GameObject boosterShuffer;
    [SerializeField] private GameObject txtBomb;
    [SerializeField] private GameObject txtShuffer;
    Action _actionHide;

    public void Init(BoosterType boosterType, Action actionHide)
    {
        txtBomb.SetActive(boosterType == BoosterType.Bomb);
        boosterBomb.SetActive(boosterType == BoosterType.Bomb);

        txtShuffer.SetActive(boosterType != BoosterType.Bomb);
        boosterShuffer.SetActive(boosterType != BoosterType.Bomb);
        _actionHide = actionHide;
    }
    public void OnClickClaim()
    {
        SoundController.Instance.PlayFX(SoundName.ClickButton);
        Hide();
        _actionHide?.Invoke();
    }
}

