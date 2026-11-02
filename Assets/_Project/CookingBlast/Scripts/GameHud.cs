using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CookingSort2D
{
    public class GameHud : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image progressFill;
        [SerializeField] private TextMeshProUGUI comboText;
        [SerializeField] private GameObject overlay;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button retryButton;

        private CookingSortGameController _game;
        private int _totalTiles;
        private Coroutine _comboPunch;
        private Vector3 _comboBaseScale = Vector3.one;
        private bool _comboScaleCached;

        private void Awake()
        {
            AutoBind();
            CacheComboScale();
        }

        public void Initialize(CookingSortGameController game, int levelNumber, int totalTiles)
        {
            _game = game;
            _totalTiles = Mathf.Max(1, totalTiles);
            AutoBind();
            CacheComboScale();
            if (levelText != null) levelText.text = "Level\n" + levelNumber;
            if (progressFill != null) progressFill.fillAmount = 0f;
            if (comboText != null)
            {
                if (_comboPunch != null) StopCoroutine(_comboPunch);
                comboText.rectTransform.localScale = _comboBaseScale;
                comboText.text = string.Empty;
            }
            if (overlay != null) overlay.SetActive(false);
            if (retryButton != null)
            {
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(() => { if (_game != null) _game.RestartLevel(); });
            }
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
            comboText.text = $"Combo\n<size=120%>{combo}</size> <size=65%>x{multiplier:0.#}</size>";
            if (_comboPunch != null) StopCoroutine(_comboPunch);
            _comboPunch = StartCoroutine(PunchComboRoutine());
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
                    punchScale, _comboBaseScale, eased);
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

public void ShowWin()
        {
            AutoBind();
            if (PopupController.Instance != null)
            {
                PopupController.Instance.Hide<PopupInGame>(PopupAnimation.None);
                Popup popup = PopupController.Instance.Get<PopupWin>();
                if (popup is PopupWin winPopup)
                {
                    winPopup.Configure(() =>
                    {
                        if (_game != null) _game.ContinueAfterWin();
                    });
                    PopupController.Instance.Show<PopupWin>(PopupAnimation.ScaleFade);
                    return;
                }
            }

            if (overlay != null) overlay.SetActive(true);
            if (resultText != null) resultText.text = "★  ★  ★\n<size=135%>Excellent!</size>\n<size=75%>Level Complete</size>";
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(true);
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(() => { if (_game != null) _game.ContinueAfterWin(); });
                TextMeshProUGUI txt = retryButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = "Next Level";
            }
        }

public void ShowFail()
        {
            AutoBind();
            if (PopupController.Instance != null)
            {
                PopupController.Instance.Hide<PopupInGame>(PopupAnimation.None);
                Popup popup = PopupController.Instance.Get<PopupLose>();
                if (popup is PopupLose losePopup)
                {
                    losePopup.Configure(() =>
                    {
                        if (_game != null) _game.RestartLevel();
                    });
                    PopupController.Instance.Show<PopupLose>(PopupAnimation.ScaleFade);
                    return;
                }
            }

            if (overlay != null) overlay.SetActive(true);
            if (resultText != null) resultText.text = "Level Failed!\n<size=70%>The table is full and no food can be served.</size>";
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(true);
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(() => { if (_game != null) _game.RestartLevel(); });
                TextMeshProUGUI txt = retryButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = "Try Again";
            }
        }

        private void AutoBind()
        {
            if (levelText == null)
            {
                Transform t = transform.Find("LevelBadge/LevelText");
                if (t != null) levelText = t.GetComponent<TextMeshProUGUI>();
            }
            if (progressFill == null)
            {
                Transform t = transform.Find("ProgressBG/ProgressFill");
                if (t != null) progressFill = t.GetComponent<Image>();
            }
            if (comboText == null)
            {
                Transform t = transform.Find("ComboText");
                if (t != null) comboText = t.GetComponent<TextMeshProUGUI>();
            }
            if (overlay == null)
            {
                Transform t = transform.Find("Overlay");
                if (t != null) overlay = t.gameObject;
            }
            if (resultText == null)
            {
                Transform t = transform.Find("Overlay/ResultText");
                if (t != null) resultText = t.GetComponent<TextMeshProUGUI>();
            }
            if (retryButton == null)
            {
                Transform t = transform.Find("Overlay/RetryButton");
                if (t != null) retryButton = t.GetComponent<Button>();
            }
        }
    }
}
