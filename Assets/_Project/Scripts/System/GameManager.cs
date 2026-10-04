using CustomTween;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : SingletonDontDestroy<GameManager>
{
    public LevelController levelController;
    public GameState gameState;
    public LoseReason loseReason;
    Sequence _sequence;
    private Coroutine _lowMemoryCleanupRoutine;
    private int _startGameRequestVersion;
    private bool _lifeConsumedForCurrentAttempt;
    private readonly List<CardType> _pendingPreLevelCards =
        new List<CardType>();

    protected override void Awake()
    {
        base.Awake();
        Application.targetFrameRate = 60;
        Input.multiTouchEnabled = true;
        CustomTweenConfig.warnZeroDuration = false;
        // CustomButton tween mau ve normalColor (1,1,1,1) tren cac graphic von da trang
        // -> spam warning "endValue equals to the current animated value". Vo hai.
        CustomTweenConfig.warnEndValueEqualsCurrent = false;
        Application.lowMemory += HandleLowMemory;
    }

    void Start()
    {
#if UNITY_EDITOR
        int requestedLevel = UnityEditor.SessionState.GetInt(Level.EditorPlayLevelKey, 0);
        UnityEditor.SessionState.EraseInt(Level.EditorPlayLevelKey);
        if (requestedLevel > 0)
        {
            Data.PlayerData.CurrentLevelIndex = requestedLevel;
            Time.timeScale = 1f;
            PrepareLevel();
            StartGame();
            return;
        }
#endif
        if (Data.PlayerData.CurrentLevelIndex <= 1)
        {
            PlayCurrentLevel(true);
        }
        else
        {
            ReturnHome();
        }
    }

    public void PlayCurrentLevel(
        bool ignorePrepareLevel = true,
        bool usePopupTransition = false
    )
    {
        if (!EnsurePlayableHeart())
        {
            return;
        }

        if (ignorePrepareLevel) PrepareLevel();
        StartGame(usePopupTransition);
    }

    public void PrepareLevel()
    {
        gameState = GameState.PrepareGame;
        levelController.PrepareLevel();
        loseReason = LoseReason.Normal;
    }

    public void ReturnHome(bool playHomeEntrance = false)
    {
        // Huy quyen bat dau cua mot request Addressables con dang cho.
        ++_startGameRequestVersion;

        // ReturnHome can be called while the same-scene reveal is still running.
        // Clear its raycast blocker before enabling the Home UI again.
        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.ResetImmediately();
        }

        PopupTransition transition =
            playHomeEntrance && PopupController.Instance != null
                ? PopupController.Instance.Get<PopupTransition>() as PopupTransition
                : null;

        if (transition != null)
        {
            transition.ShowAndCover();
            transition.PlayReveal(
                onNearFinish: () => ApplyReturnHome(
                    playHomeEntrance,
                    transition
                )
            );
            return;
        }

        ApplyReturnHome(playHomeEntrance);
    }

    private void ApplyReturnHome(
        bool playHomeEntrance,
        PopupTransition transition = null
    )
    {
        PrepareLevel();

        SoundController.Instance.PlayBackground(SoundName.HomeBackgroundMusic);
        if (transition != null)
            PopupController.Instance.HideAllExcept<PopupTransition>();
        else
            PopupController.Instance.HideAll();

        PopupController.Instance.Show<PopupBackground>();

        if (playHomeEntrance)
            PopupController.Instance.ShowHomeWithEntrance();
        else
            PopupController.Instance.Show<PopupHome>();
    }

    public void ReplayGame()
    {
        if (!EnsurePlayableHeart())
        {
            return;
        }

        Observer.ReplayLevel?.Invoke(levelController.currentLevel);
        PrepareLevel();
        StartGame();
    }

    public void ReplayGamePausedWithPopupBoosterQuit()
    {
        ConsumeLifeForCurrentAttempt();
        Time.timeScale = 0f;
        PopupController.Instance.HideAll();

        if (!HasPlayableHeart())
        {
            Time.timeScale = 1f;
            ReturnHome();
            PopupController.Instance.Show<PopupMoreLife>(PopupAnimation.None);
            return;
        }

        if (PopupController.Instance.Get<PopupBoosterQuit>() is PopupBoosterQuit popupBoosterQuit)
        {
            popupBoosterQuit.Show(PopupAnimation.None);
        }
        else
        {
            Debug.LogWarning("[GameManager] PopupBoosterQuit chua co trong PopupConfig.");
        }
    }

    public void StartPreparedLevelFromPopupBoosterQuit()
    {
        Time.timeScale = 1f;
        ReplayGame();
    }

    public void BackLevel()
    {
        if (!EnsurePlayableHeart())
        {
            return;
        }

        Data.PlayerData.CurrentLevelIndex--;
        Data.SaveData();

        PrepareLevel();
        StartGame();
    }

    public void NextLevel()
    {
        if (!EnsurePlayableHeart())
        {
            return;
        }

        Observer.SkipLevel?.Invoke(levelController.currentLevel);
        Data.PlayerData.CurrentLevelIndex++;
        Data.SaveData();

        PrepareLevel();
        StartGame();
    }

    public async void StartGame(bool usePopupTransition = false)
    {
        int requestVersion = ++_startGameRequestVersion;

        PopupTransition transition =
            usePopupTransition && PopupController.Instance != null
                ? PopupController.Instance.Get<PopupTransition>() as PopupTransition
                : null;

        if (transition != null)
            transition.ShowAndCover();

        if (levelController == null)
        {
            Debug.LogError("[GameManager] levelController chua duoc gan!");
            transition?.PlayReveal();
            return;
        }

        Level level;
        try
        {
            // Neu PrepareLevel da bat dau load thi lenh nay se cho dung operation do.
            // Neu chua co level, no tu load Addressable tuong ung.
            level = await levelController.PrepareLevelAsync(false);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GameManager] Load level that bai: {exception.Message}");
            if (requestVersion == _startGameRequestVersion)
                transition?.PlayReveal();
            return;
        }

        // Trong luc await, nguoi choi co the da bam Home hoac chon level khac.
        if (requestVersion != _startGameRequestVersion)
        {
            return;
        }

        if (level == null)
        {
            Debug.LogError(
                $"[GameManager] Khong load duoc Level {Data.PlayerData.CurrentLevelIndex} " +
                "(kiem tra Addressables group Levels va address 'Level X').");
            if (requestVersion == _startGameRequestVersion)
                transition?.PlayReveal();
            return;
        }

        if (transition != null)
        {
            transition.PlayReveal(
                onNearFinish: () =>
                {
                    if (requestVersion != _startGameRequestVersion)
                        return;

                    PresentGame(level, transition);
                }
            );
            return;
        }

        PresentGame(level);
    }

    private void PresentGame(
        Level level,
        PopupTransition transition = null
    )
    {
        _lifeConsumedForCurrentAttempt = false;
        gameState = GameState.PlayingGame;

        // Doi popup TRUOC khi ban Observer.StartLevel.
        // Truoc day Observer.StartLevel chay dau tien, nen chi can 1 subscriber
        // nem exception la HideAll()/Show<PopupInGame>() khong bao gio chay
        // => bam Play khong thay gi xay ra.
        SoundController.Instance.PlayBackground(SoundName.InGameBackgroundMusic);
        if (transition != null)
            PopupController.Instance.HideAllExcept<PopupTransition>();
        else
            PopupController.Instance.HideAll();

        PopupController.Instance.Show<PopupInGame>();
        level.gameObject.SetActive(true);

        // Apply synchronously, then reuse the same list for the next level.
        // Creating a copy here caused avoidable managed allocations each time
        // a level started.
        try
        {
            level.ApplySelectedPreLevelCards(_pendingPreLevelCards);
        }
        finally
        {
            _pendingPreLevelCards.Clear();
        }

        Observer.StartLevel?.Invoke(level);
        //FirebaseController.Instance.TrackingStartLevel(levelController.currentLevel.name);

    }

    public bool ConsumeLifeForCurrentAttempt()
    {
        if (_lifeConsumedForCurrentAttempt)
            return true;

        HeartController heartController = HeartController.Instance;
        if (heartController == null || !heartController.TryConsumeHeart())
            return false;

        _lifeConsumedForCurrentAttempt = true;
        return true;
    }

    private static bool HasPlayableHeart()
    {
        return HeartController.Instance != null
            ? HeartController.Instance.HasPlayableHeart
            : Data.PlayerData != null &&
              (Data.PlayerData.IsInfiniteHeart() ||
               Data.PlayerData.CurrentHeart > 0);
    }

    private static bool EnsurePlayableHeart()
    {
        if (HasPlayableHeart())
            return true;

        Observer.Notify?.Invoke("Not enough heart!", Vector3.zero);
        if (PopupController.Instance != null)
            PopupController.Instance.Show<PopupMoreLife>(PopupAnimation.None);

        return false;
    }

    public void QueuePreLevelCards(IEnumerable<CardType> cardTypes)
    {
        if (cardTypes == null)
            return;

        foreach (CardType cardType in cardTypes)
            _pendingPreLevelCards.Add(cardType);
    }

    public void OnWinGame(float delayPopupShowTime = 1f)
    {
        if (gameState == GameState.WaitingResult || gameState == GameState.LoseGame || gameState == GameState.WinGame) return;
        gameState = GameState.WinGame;

        PopupInGame popupInGame = PopupController.Instance != null
            ? PopupController.Instance.Get<PopupInGame>() as PopupInGame
            : null;
        popupInGame?.HideSettingsButtonForWin();

        Observer.WinLevel?.Invoke(levelController.currentLevel);
        Data.PlayerData.CountShowInterAds++;
        //FirebaseController.Instance.TrackingWinLevel(levelController.currentLevel.name);
        Data.PlayerData.CurrentLevelIndex++;
        Data.SaveData();
        // Data.PlayerData.SavingReward = new RewardData(10, 0);
        _sequence = Sequence.Create().ChainDelay(delayPopupShowTime).ChainCallback(() =>
        {
            PopupController.Instance.HideAll();
            if (PopupController.Instance.Get<PopupWin>() is PopupWin popupWin)
            {
                popupWin.Show();
            }
        });
    }

    public void OnLoseGame(
        float delayPopupShowTime = 2.5f,
        LoseReason loseReason = LoseReason.Normal,
        bool allowContinue = true)
    {
        if (gameState == GameState.WaitingResult || gameState == GameState.LoseGame || gameState == GameState.WinGame) return;
        gameState = GameState.LoseGame;
        SoundController.Instance?.PauseBackground();
        SoundController.Instance?.PlayFX(SoundName.LoseLevel);
        this.loseReason = loseReason;
        Observer.LoseLevel?.Invoke(levelController.currentLevel);
        Data.PlayerData.CountShowInterAds++;
        //FirebaseController.Instance.TrackingLoseLevel(levelController.currentLevel.name);
        _sequence = Sequence.Create().ChainDelay(delayPopupShowTime).ChainCallback(() =>
        {
            if (!allowContinue ||
                Data.PlayerData.CurrentLevelIndex <= 4)
            {
                PopupController.Instance.Show<PopupLose>();
            }
            else
            {
                // PopupController.Instance.HideAll();
                PopupController.Instance.Hide<PopupInGame>();
                PopupController.Instance.Show<PopupContinue>();
            }
        });
    }

    public void ShowContinueWarning()
    {
        if (gameState != GameState.PlayingGame)
            return;

        gameState = GameState.WaitingResult;

        PopupInGame popupInGame = PopupController.Instance != null
            ? PopupController.Instance.Get<PopupInGame>() as PopupInGame
            : null;

        if (popupInGame != null &&
            popupInGame.PlayOutOfMoveTransition(
                ShowContinuePopupAfterOutOfMove
            ))
        {
            return;
        }

        ShowContinuePopupAfterOutOfMove();
    }

    private void ShowContinuePopupAfterOutOfMove()
    {
        if (gameState != GameState.WaitingResult)
            return;

        PopupController.Instance.Hide<PopupInGame>();
        PopupController.Instance.Show<PopupContinue>();
    }

    public void ConfirmLoseAfterWarning()
    {
        if (gameState == GameState.WaitingResult)
        {
            // OnLoseGame only starts from PlayingGame. Returning to this
            // state here lets the normal lose bookkeeping run exactly once.
            gameState = GameState.PlayingGame;
            OnLoseGame(
                0f,
                LoseReason.OutMove,
                allowContinue: false
            );
            return;
        }

        PopupController.Instance.Show<PopupLose>();
    }
   
    public void CallResume()
    {
        gameState = GameState.PlayingGame;
        SoundController.Instance?.PlayBackground(SoundName.InGameBackgroundMusic);
        PopupController.Instance.Show<PopupInGame>();
    }
    public void CallAddBombAndResume()
    {
        Data.PlayerData.CurrentBomb++;
        var popupInGame = PopupController.Instance.Get<PopupInGame>();
        var inGameBoosters = popupInGame.GetComponentsInChildren<InGameBoosterItem>();
        for (int i = 0; i < inGameBoosters.Length; i++)
        {
            if (inGameBoosters[i].BoosterType == BoosterType.Bomb)
            {
                inGameBoosters[i].OnClickBooster();
                break;
            }
        }
        CallResume();
    }
    void OnDisable()
    {
        _sequence.Stop();
    }

    private void OnDestroy()
    {
        Application.lowMemory -= HandleLowMemory;
    }

    private void HandleLowMemory()
    {
        if (_lowMemoryCleanupRoutine == null)
            _lowMemoryCleanupRoutine = StartCoroutine(RecoverFromLowMemory());
    }

    private IEnumerator RecoverFromLowMemory()
    {
        // Let the current UI update complete before releasing hidden popup objects.
        yield return null;

        PopupController popupController = PopupController.Instance;
        if (popupController != null)
            popupController.ReleaseInactivePopups();

        yield return Resources.UnloadUnusedAssets();
        GC.Collect();
        _lowMemoryCleanupRoutine = null;
    }
}

public enum GameState
{
    PrepareGame,
    PlayingGame,
    WaitingResult,
    LoseGame,
    WinGame,
}
public enum LoseReason
{
    Normal,
    OutMove,
    BoardFull,
}
