using System.Collections;
using CookingSort2D;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupInGame : Popup
{
    [Header("Pause Menu")]
    [SerializeField] private InGamePauseMenu pauseMenu;

    [Header("Cooking Blast HUD")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Image progressFill;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private GameObject resultOverlay;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button retryButton;

    [Header("Cooking Blast Boosters")]
    [SerializeField] private GameObject orderBellButton;
    [SerializeField] private GameObject lineUpButton;
    [SerializeField] private GameObject rewindButton;
    [SerializeField] private GameObject trashAwayButton;

    private CookingSortGameController _game;
    private int _totalTiles;
    private Coroutine _comboPunch;
    private Vector3 _comboBaseScale = Vector3.one;
    private bool _comboScaleCached;

    protected override void OnInstantiate()
    {
        base.OnInstantiate();
        AutoBindHud();
        CacheComboScale();
        BindBoosterButton(orderBellButton, OnClickOrderBell);
        BindBoosterButton(lineUpButton, OnClickLineUp);
        BindBoosterButton(rewindButton, OnClickRewind);
        BindBoosterButton(trashAwayButton, OnClickTrashAway);
        if (pauseMenu != null) pauseMenu.CloseImmediate();
    }

    public void Initialize(
        CookingSortGameController game,
        int levelNumber,
        int totalTiles)
    {
        _game = game;
        _totalTiles = Mathf.Max(1, totalTiles);

        AutoBindHud();
        CacheComboScale();

        if (levelText != null) levelText.text = "Level\n" + levelNumber;
        if (progressFill != null) progressFill.fillAmount = 0f;
        if (comboText != null)
        {
            if (_comboPunch != null) StopCoroutine(_comboPunch);
            comboText.rectTransform.localScale = _comboBaseScale;
            comboText.text = string.Empty;
        }

        if (resultOverlay != null) resultOverlay.SetActive(false);
        BindRetryAction(() =>
        {
            if (_game != null) _game.RestartLevel();
        });
    }

    public void SetProgress(int served)
    {
        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(served / (float)_totalTiles);
    }

    public void SetCombo(int combo, float multiplier)
    {
        if (comboText == null) return;

        if (combo <= 0)
        {
            if (_comboPunch != null) StopCoroutine(_comboPunch);
            comboText.rectTransform.localScale = _comboBaseScale;
            comboText.text = string.Empty;
            return;
        }

        comboText.text =
            $"Combo\n<size=120%>{combo}</size> <size=65%>x{multiplier:0.#}</size>";

        if (_comboPunch != null) StopCoroutine(_comboPunch);
        _comboPunch = StartCoroutine(PunchComboRoutine());
    }

    public void ShowWin()
    {
        AutoBindHud();
        if (PopupController.Instance != null)
        {
            if (PopupController.Instance.Get<PopupWin>() is PopupWin winPopup)
            {
                winPopup.Configure(() =>
                {
                    if (_game != null) _game.ContinueAfterWin();
                });
                PopupController.Instance.Hide<PopupInGame>(PopupAnimation.None);
                PopupController.Instance.Show<PopupWin>(PopupAnimation.ScaleFade);
                return;
            }
        }

        if (resultOverlay != null) resultOverlay.SetActive(true);
        if (resultText != null)
        {
            resultText.text =
                "★  ★  ★\n<size=135%>Excellent!</size>\n" +
                "<size=75%>Level Complete</size>";
        }

        SetRetryButton("Next Level", () =>
        {
            if (_game != null) _game.ContinueAfterWin();
        });
    }

    public void ShowFail()
    {
        AutoBindHud();
        if (PopupController.Instance != null)
        {
            if (PopupController.Instance.Get<PopupLose>() is PopupLose losePopup)
            {
                losePopup.Configure(() =>
                {
                    if (_game != null) _game.RestartLevel();
                });
                PopupController.Instance.Hide<PopupInGame>(PopupAnimation.None);
                PopupController.Instance.Show<PopupLose>(PopupAnimation.ScaleFade);
                return;
            }
        }

        if (resultOverlay != null) resultOverlay.SetActive(true);
        if (resultText != null)
        {
            resultText.text =
                "Level Failed!\n<size=70%>The table is full and no food can be served.</size>";
        }

        SetRetryButton("Try Again", () =>
        {
            if (_game != null) _game.RestartLevel();
        });
    }

    public void OnClickSetting()
    {
        if (pauseMenu != null) pauseMenu.Open();
    }

    public void OnClickOrderBell() => UseBooster(BoosterType.OrderBell);
    public void OnClickLineUp() => UseBooster(BoosterType.LineUp);
    public void OnClickRewind() => UseBooster(BoosterType.Rewind);
    public void OnClickTrashAway() => UseBooster(BoosterType.TrashAway);

    public void OnClickCancelBooster()
    {
        if (_game != null) _game.CancelActiveBooster();
    }

    private void UseBooster(BoosterType type)
    {
        if (_game == null)
            _game = UnityEngine.Object.FindFirstObjectByType<CookingSort2D.CookingSortGameController>();
        if (_game != null) _game.TryUseBooster(type);
    }

    private void BindBoosterButton(
        UnityEngine.GameObject buttonObject,
        UnityEngine.Events.UnityAction callback)
    {
        if (buttonObject == null) return;
        CustomButton button = buttonObject.GetComponent<CustomButton>();
        if (button == null) button = buttonObject.AddComponent<CustomButton>();
        button.Click.RemoveListener(callback);
        button.Click.AddListener(callback);
    }

    private IEnumerator PunchComboRoutine()
    {
        RectTransform target = comboText.rectTransform;
        Vector3 punchScale = _comboBaseScale * 1.16f;
        target.localScale = punchScale;

        float elapsed = 0f;
        const float duration = 0.09f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            target.localScale = Vector3.LerpUnclamped(
                punchScale,
                _comboBaseScale,
                eased);
            yield return null;
        }

        target.localScale = _comboBaseScale;
        _comboPunch = null;
    }

    private void CacheComboScale()
    {
        if (_comboScaleCached || comboText == null) return;
        _comboBaseScale = comboText.rectTransform.localScale;
        _comboScaleCached = true;
    }

    private void BindRetryAction(UnityEngine.Events.UnityAction action)
    {
        if (retryButton == null) return;
        retryButton.onClick.RemoveAllListeners();
        retryButton.onClick.AddListener(action);
    }

    private void SetRetryButton(
        string label,
        UnityEngine.Events.UnityAction action)
    {
        if (retryButton == null) return;

        retryButton.gameObject.SetActive(true);
        BindRetryAction(action);
        TextMeshProUGUI text =
            retryButton.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null) text.text = label;
    }

    private void AutoBindHud()
    {
        Transform hudRoot = transform.Find("CookingBlastHud");
        if (hudRoot == null) hudRoot = transform;

        if (levelText == null)
        {
            Transform target = hudRoot.Find("LevelBadge/LevelText");
            if (target == null)
                target = hudRoot.Find("ProgressBG/LevelText");
            if (target != null) levelText = target.GetComponent<TextMeshProUGUI>();
        }

        if (progressFill == null)
        {
            Transform target = hudRoot.Find("ProgressBG/ProgressFill");
            if (target != null) progressFill = target.GetComponent<Image>();
        }

        if (comboText == null)
        {
            Transform target = hudRoot.Find("ComboText");
            if (target != null) comboText = target.GetComponent<TextMeshProUGUI>();
        }

        if (resultOverlay == null)
        {
            Transform target = hudRoot.Find("Overlay");
            if (target != null) resultOverlay = target.gameObject;
        }

        if (resultText == null)
        {
            Transform target = hudRoot.Find("Overlay/ResultText");
            if (target != null) resultText = target.GetComponent<TextMeshProUGUI>();
        }

        if (retryButton == null)
        {
            Transform target = hudRoot.Find("Overlay/RetryButton");
            if (target != null) retryButton = target.GetComponent<Button>();
        }
    }
}
