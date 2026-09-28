using System.Collections;
using CustomTween;
using Lean.Pool;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class PopupInGame : Popup
{
    [Header("Level UI")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("Star UI")]
    [SerializeField] private TextMeshProUGUI starText;
    [SerializeField] private GameObject starTarget;
    [SerializeField] private GameObject starPrefab;

    [Header("Pause Menu")]
    [SerializeField] private InGamePauseMenu pauseMenu;
    
    [Header("Legacy Target UI (hidden for Differences)")]
    [SerializeField] private Transform targetGroup;

    [Header("Progress UI")]
    [SerializeField] private TextMeshProUGUI dealText;

    [Header("Find the Differences")]
    [SerializeField] private TextMeshProUGUI mistakesText;
    [SerializeField] private GameObject[] legacyBoosterObjects;

    private int _currentStar;
    private Level _currentLevel;

    public int CurrentStar
    {
        get => _currentStar;
        set
        {
            _currentStar = value;

            if (starText != null)
            {
                starText.text = _currentStar.ToString();
            }
        }
    }

    private void Awake()
    {
        Observer.StartLevel += StartLevel;
    }

    private void OnDestroy()
    {
        Observer.StartLevel -= StartLevel;
        if (_currentLevel != null)
            _currentLevel.OnBoardChanged -= UpdateTargetsUI;
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();

        Setup();
    }

    private void Setup()
    {
        if (levelText != null)
        {
            levelText.text =
                $"Level {GameManager.Instance.ActiveLevelNumber}" + (GameManager.Instance.IsPictureReplay ? " · Replay" : "");
        }
    }

    private void StartLevel(Level level)
    {
        if (_currentLevel != null)
        {
            _currentLevel.OnBoardChanged -= UpdateTargetsUI;
        }

        _currentLevel = level;

        if (_currentLevel != null)
        {
            _currentLevel.OnBoardChanged += UpdateTargetsUI;
        }

        CurrentStar = 0;
        
        SetupTargets();
        
        _currentLevel?.SetVisible(true);
        SetLegacyBoosterVisibility(false);
        UpdateDifferenceProgress();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        Observer.FoodBoxCompleted += FoodBoxCompleted;
        if (_currentLevel != null)
        {
            _currentLevel.OnBoardChanged -= UpdateTargetsUI;
            _currentLevel.OnBoardChanged += UpdateTargetsUI;
            _currentLevel.SetPaused(false);
            _currentLevel.SetVisible(true);
            UpdateDifferenceProgress();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        Observer.FoodBoxCompleted -= FoodBoxCompleted;

        if (_currentLevel != null)
        {
            _currentLevel.SetPaused(true);
            _currentLevel.SetVisible(false);
            _currentLevel.OnBoardChanged -= UpdateTargetsUI;
        }
    }

    private void UpdateDealCount(int count)
    {
        UpdateDifferenceProgress();
    }

    private void SetupTargets()
    {
        // LeanPoolClear() da tra het target cu ve pool.
        // Khong duoc Destroy() chung nua, neu khong pool se giu clone da bi huy
        // va lan Spawn sau se warning "pool contained a null despawned clone".
        if (targetGroup != null) targetGroup.LeanPoolClear();

        if (targetGroup != null)
            targetGroup.gameObject.SetActive(false);

        UpdateDifferenceProgress();
    }

    private void UpdateTargetsUI()
    {
        UpdateDifferenceProgress();
    }

    private void UpdateDifferenceProgress()
    {
        if (dealText == null ||
            _currentLevel == null ||
            _currentLevel.Differences == null)
        {
            return;
        }

        var board = _currentLevel.Differences;
        dealText.text = $"FOUND  {board.FoundCount}/{board.TotalCount}";
        if (mistakesText != null)
            mistakesText.text = board.MaxMistakes == 0
                ? $"MISTAKES  {board.MistakeCount}"
                : $"CHANCES  {board.RemainingMistakes}/{board.MaxMistakes}";
    }

    private void SetLegacyBoosterVisibility(bool visible)
    {
        if (legacyBoosterObjects == null) return;
        for (int i = 0; i < legacyBoosterObjects.Length; i++)
        {
            if (legacyBoosterObjects[i] != null)
                legacyBoosterObjects[i].SetActive(visible);
        }
    }

    public void OnClickHint() => _currentLevel?.RequestHint();

    private void FoodBoxCompleted(Vector3 position)
    {
        StartCoroutine(
            SpawnStarsWithDelay(position)
        );
    }

    private IEnumerator SpawnStarsWithDelay(
        Vector3 position)
    {
        for (int i = 0; i < 2; i++)
        {
            PopupController.Instance.StartCoroutine(
                SpawnAndFlyStar(position)
            );

            yield return new WaitForSeconds(0.05f);
        }
    }

    private IEnumerator SpawnAndFlyStar(
        Vector3 position)
    {
        if (starPrefab == null ||
            starTarget == null)
        {
            yield break;
        }

        if (PopupController.Instance == null)
        {
            yield break;
        }

        Canvas canvas =
            PopupController.Instance
                .CanvasTransform
                .GetComponent<Canvas>();

        if (canvas == null)
        {
            yield break;
        }

        Camera cam =
            canvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

        if (cam == null)
        {
            cam = Camera.main;
        }

        Camera worldCamera = Camera.main;

        if (worldCamera == null)
        {
            yield break;
        }

        Vector3 screenPos =
            worldCamera.WorldToScreenPoint(position);

        RectTransform canvasRect =
            canvas.GetComponent<RectTransform>();

        Vector3 spawnWorldPos = position;

        if (RectTransformUtility
            .ScreenPointToWorldPointInRectangle(
                canvasRect,
                screenPos,
                cam,
                out Vector3 worldPoint))
        {
            spawnWorldPos = worldPoint;
        }

        Transform canvasRoot =
            PopupController.Instance.CanvasTransform;

        GameObject star =
            LeanPool.Spawn(
                starPrefab,
                canvasRoot
            );

        star.transform.position =
            spawnWorldPos;

        Vector3 startPos =
            star.transform.position;

        Vector3 targetPos =
            starTarget.transform.position;

        Vector3 midPoint =
            (startPos + targetPos) / 2f;

        Vector3 direction =
            (targetPos - startPos).normalized;

        Vector3 perpendicular =
            new Vector3(
                -direction.y,
                direction.x,
                0f
            );

        float distance =
            Vector3.Distance(
                startPos,
                targetPos
            );

        float offsetLen =
            Random.Range(
                distance * 0.05f,
                distance * 0.15f
            );

        Vector3 controlPoint =
            midPoint +
            perpendicular * -offsetLen;

        float duration =
            Random.Range(
                0.5f,
                0.7f
            );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );

            Vector3 m1 =
                Vector3.Lerp(
                    startPos,
                    controlPoint,
                    t
                );

            Vector3 m2 =
                Vector3.Lerp(
                    controlPoint,
                    targetPos,
                    t
                );

            star.transform.position =
                Vector3.Lerp(
                    m1,
                    m2,
                    t
                );

            yield return null;
        }

        star.transform.position =
            targetPos;

        Tween.PunchScale(
            starTarget.transform,
            Vector3.one,
            0.2f,
            1
        );

        CurrentStar++;

        LeanPool.Despawn(star);
    }

    // =========================================================
    // PAUSE MENU
    // =========================================================

    public void OnClickSetting()
    {
        var controller = PopupController.Instance;
        if (controller == null || controller.currentPopup != this ||
            GameManager.Instance.gameState != GameState.PlayingGame)
            return;

        if (controller.Get<PopupSetting>() == null)
        {
            Debug.LogWarning("[PopupInGame] PopupSetting is missing from PopupConfig.");
            return;
        }

        SoundController.Instance.PlayFX(SoundName.ClickButton);
        if (pauseMenu != null && pauseMenu.IsOpen) pauseMenu.CloseImmediately();
        controller.Show<PopupSetting>(PopupAnimation.None);
    }

    // =========================================================
    // DEBUG / GAME CONTROL
    // =========================================================

    public void OnClickReplay()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        // Đảm bảo không bị giữ TimeScale = 0
        Time.timeScale = 1f;

        GameManager.Instance.ReplayGame();
    }

    public void OnClickPrevious()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        Time.timeScale = 1f;

        GameManager.Instance.BackLevel();
    }

    public void OnClickSkip()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        Time.timeScale = 1f;

        GameManager.Instance.NextLevel();
    }

    public void OnClickLose()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        Time.timeScale = 1f;

        GameManager.Instance.ShowContinueWarning();
    }

    public void OnClickWin()
    {
        SoundController.Instance.PlayFX(
            SoundName.ClickButton
        );

        Time.timeScale = 1f;

        GameManager.Instance.OnWinGame(1f);
    }
}
