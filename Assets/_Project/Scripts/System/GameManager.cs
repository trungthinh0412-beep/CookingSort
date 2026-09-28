using CustomTween;
using System;
using UnityEngine;

public class GameManager : SingletonDontDestroy<GameManager>
{
    public LevelController levelController;
    public GameState gameState;
    public LoseReason loseReason;
    [SerializeField] private FeatureConfig featureConfig;
    Sequence _sequence;
    private int _startGameRequestVersion;
    private PicturePlaySession pictureSession;
    public bool IsPictureReplay => pictureSession != null && pictureSession.IsReplay;
    public bool CanClaimLevelReward => pictureSession == null || pictureSession.CanClaimReward;
    public int ActiveLevelNumber => pictureSession != null ? pictureSession.LevelNumber : Data.PlayerData.CurrentLevelIndex;

    protected override void Awake()
    {
        base.Awake();
        Application.targetFrameRate = 60;
        Input.multiTouchEnabled = true;
        CustomTweenConfig.warnZeroDuration = false;
        // CustomButton tween mau ve normalColor (1,1,1,1) tren cac graphic von da trang
        // -> spam warning "endValue equals to the current animated value". Vo hai.
        CustomTweenConfig.warnEndValueEqualsCurrent = false;
    }

    void Start()
    {
        if (levelController.PictureCollection != null && levelController.PictureCollection.MigrateProgress(Data.PlayerData))
            Data.SaveData();
        ReturnHome();
    }

    public void PlayCurrentLevel(
        bool ignorePrepareLevel = true,
        bool usePopupTransition = false
    )
    {
        _sequence.Stop();
        pictureSession = null;
        if (Data.PlayerData.CurrentHeart <= 0)
        {
            Observer.Notify?.Invoke("Not enough heart!", Vector3.zero);
            return;
        }

        if (ignorePrepareLevel) PrepareLevel();
        StartGame(usePopupTransition);
    }

    public void PrepareLevel()
    {
        levelController.currentLevel?.SetVisible(false);
        gameState = GameState.PrepareGame;
        if (IsPictureReplay) _ = levelController.PreparePictureLevelAsync(pictureSession.Entry, true);
        else levelController.PrepareLevel();
        loseReason = LoseReason.Normal;
    }

    public void ReturnHome(bool playHomeEntrance = false)
    {
        if (IsPictureReplay) { ExitPictureReplay(); return; }
        _sequence.Stop();
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
        _sequence.Stop();
        if (!IsPictureReplay && Data.PlayerData.CurrentHeart <= 0)
        {
            Observer.Notify?.Invoke("Not enough heart!", Vector3.zero);
            return;
        }

        Observer.ReplayLevel?.Invoke(levelController.currentLevel);
        PrepareLevel();
        StartGame();
    }

    public async void ReplayGamePausedWithPopupBoosterQuit()
    {
        if (IsPictureReplay) { Time.timeScale = 1; ReplayGame(); return; }
        Time.timeScale = 0f;
        PopupController.Instance.HideAll();

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
        if (IsPictureReplay) return;
        if (Data.PlayerData.CurrentHeart <= 0)
        {
            Observer.Notify?.Invoke("Not enough heart!", Vector3.zero);
            return;
        }

        Data.PlayerData.CurrentLevelIndex--;
        Data.SaveData();

        PrepareLevel();
        StartGame();
    }

    public void NextLevel()
    {
        if (IsPictureReplay) return;
        if (Data.PlayerData.CurrentHeart <= 0)
        {
            Observer.Notify?.Invoke("Not enough heart!", Vector3.zero);
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
        int mainLevel = Data.PlayerData.CurrentLevelIndex;

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
            level = IsPictureReplay
                ? await levelController.PreparePictureLevelAsync(pictureSession.Entry)
                : await levelController.PrepareLevelAsync(false);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GameManager] Load level that bai: {exception.Message}");
            if (requestVersion == _startGameRequestVersion)
            {
                transition?.PlayReveal();
                if (IsPictureReplay) ExitPictureReplay("Could not load this picture. Please try again.");
            }
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
            {
                transition?.PlayReveal();
                if (IsPictureReplay) ExitPictureReplay("Could not load this picture. Please try again.");
            }
            return;
        }

        if (!IsPictureReplay)
        {
            var entry = levelController.LoadedPictureEntry;
            pictureSession = new PicturePlaySession(levelController.PictureCollection?.FindAlbum(entry), entry, mainLevel, false);
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

        Observer.StartLevel?.Invoke(level);
        level.BeginLevel(
            () => OnWinGame(),
            () => OnLoseGame(0.5f, LoseReason.OutMove, allowContinue: false));
        //FirebaseController.Instance.TrackingStartLevel(levelController.currentLevel.name);

        // Check if this level unlocks a new feature
        if (!IsPictureReplay) CheckAndShowNewFeature();
    }

    public void PlayPictureReplay(PictureAlbumData album, PictureLevelEntry entry)
    {
        var collection = levelController.PictureCollection;
        if (PopupController.Instance.IsPicturePageSliding || collection == null || album == null || entry == null ||
            !collection.albums.Contains(album) || !album.levels.Contains(entry) || !entry.IsPlayable ||
            !Data.PlayerData.HasCompletedPicture(entry.levelId)) return;
        _sequence.Stop();
        Time.timeScale = 1;
        pictureSession = new PicturePlaySession(album, entry, entry.levelNumber, true);
        PrepareLevel();
        StartGame(true);
    }

    public void ExitPictureReplay(string message = "")
    {
        if (!IsPictureReplay) return;
        ++_startGameRequestVersion;
        _sequence.Stop();
        var session = pictureSession;
        pictureSession = null;
        gameState = GameState.PrepareGame;
        Time.timeScale = 1;
        levelController.currentLevel?.SetPaused(true);
        levelController.currentLevel?.SetVisible(false);
        TransitionManager.Instance?.ResetImmediately();
        SoundController.Instance.PlayBackground(SoundName.HomeBackgroundMusic);
        PopupController.Instance.ShowPictureReplayReturn(session.Album, session.Entry, message);
    }

    private void CheckAndShowNewFeature()
    {
        if (featureConfig == null) return;

        int currentLevel = Data.PlayerData.CurrentLevelIndex;
        FeatureData featureData = featureConfig.GetFeatureDataAtLevel(currentLevel);

        if (featureData == null) return;

        if (PopupController.Instance.Get<PopupNewFeature>() is PopupNewFeature popup)
        {
            popup.Setup(featureData);
            popup.Show(PopupAnimation.ScaleFade);
        }
        else
        {
            Debug.LogWarning("[GameManager] PopupNewFeature chua co trong PopupConfig.");
        }
    }
    public void OnWinGame(float delayPopupShowTime = 2.5f)
    {
        if (gameState == GameState.WaitingResult || gameState == GameState.LoseGame || gameState == GameState.WinGame) return;
        gameState = GameState.WinGame;
        int resultVersion = _startGameRequestVersion;
        if (IsPictureReplay)
        {
            _sequence = Sequence.Create().ChainDelay(.6f).ChainCallback(() =>
            {
                if (resultVersion == _startGameRequestVersion) ExitPictureReplay("Completed! Replay anytime.");
            });
            return;
        }
        Observer.WinLevel?.Invoke(levelController.currentLevel);
        //FirebaseController.Instance.TrackingWinLevel(levelController.currentLevel.name);
        pictureSession?.Complete(Data.PlayerData);
        Data.SaveData();
        // Data.PlayerData.SavingReward = new RewardData(10, 0);
        _sequence = Sequence.Create().ChainDelay(delayPopupShowTime).ChainCallback(() =>
        {
            if (resultVersion != _startGameRequestVersion) return;
            PopupController.Instance.HideAll();
            PopupController.Instance.Show<PopupWin>();
        });
    }

    public void OnLoseGame(
        float delayPopupShowTime = 2.5f,
        LoseReason loseReason = LoseReason.Normal,
        bool allowContinue = true)
    {
        if (gameState == GameState.WaitingResult || gameState == GameState.LoseGame || gameState == GameState.WinGame) return;
        gameState = GameState.LoseGame;
        this.loseReason = loseReason;
        int resultVersion = _startGameRequestVersion;
        if (IsPictureReplay)
        {
            _sequence = Sequence.Create().ChainDelay(.5f).ChainCallback(() =>
            {
                if (resultVersion == _startGameRequestVersion) ExitPictureReplay("Try again — replay is free.");
            });
            return;
        }
        Observer.LoseLevel?.Invoke(levelController.currentLevel);
        Data.PlayerData.CountShowInterAds++;
        //FirebaseController.Instance.TrackingLoseLevel(levelController.currentLevel.name);
        _sequence = Sequence.Create().ChainDelay(delayPopupShowTime).ChainCallback(() =>
        {
            if (resultVersion != _startGameRequestVersion) return;
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

        Level level = levelController != null
            ? levelController.currentLevel
            : null;

        if (!PopupContinue.HasAvailableOption(level))
        {
            OnLoseGame(
                0f,
                LoseReason.OutMove,
                allowContinue: false
            );
            return;
        }

        gameState = GameState.WaitingResult;
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
}
