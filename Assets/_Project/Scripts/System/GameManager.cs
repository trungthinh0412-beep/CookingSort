using CustomTween;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { None }

public partial class GameManager : SingletonDontDestroy<GameManager>
{
    public LevelController levelController;
    public GameState gameState = GameState.None; 
    public void PrepareLevel() {} 
    public void StartGame() {}

    protected override void Awake()
    {
        base.Awake();
        Application.targetFrameRate = 60;
        Input.multiTouchEnabled = true;
        CustomTweenConfig.warnZeroDuration = false;
        CustomTweenConfig.warnEndValueEqualsCurrent = false;
    }

    void Start()
    {
        ReturnHome();
    }

    public void ReturnHome(bool playHomeEntrance = false)
    {
        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.ResetImmediately();
        }

        if (PopupController.Instance != null)
        {
            PopupController.Instance.HideAll();
            PopupController.Instance.Show<PopupBackground>();
            PopupController.Instance.Show<PopupHome>();
        }
    }
}public partial class GameManager { public static int PlayingGame = 0; }
