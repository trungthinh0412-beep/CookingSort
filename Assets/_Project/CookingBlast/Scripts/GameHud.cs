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

        private void Awake()
        {
            AutoBind();
        }

        public void Initialize(CookingSortGameController game, int levelNumber, int totalTiles)
        {
            _game = game;
            _totalTiles = Mathf.Max(1, totalTiles);
            AutoBind();
            if (levelText != null) levelText.text = "Level\n" + levelNumber;
            if (progressFill != null) progressFill.fillAmount = 0f;
            if (comboText != null) comboText.text = string.Empty;
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
                comboText.text = string.Empty;
                return;
            }
            comboText.text = $"Combo\n<size=120%>{combo}</size> <size=65%>x{multiplier:0.#}</size>";
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
