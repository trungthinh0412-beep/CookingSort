using CustomTween;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingController : MonoBehaviour
{
    [Header("Attributes")]
    [SerializeField] private float timeLoading = 5f;

    [Header("Components")]
    [SerializeField] private Image progress;
    [SerializeField] private TextMeshProUGUI txtFill;
    [SerializeField] private TextMeshProUGUI txtLoading;

    [Header("Loading Text")]
    [SerializeField] private float dotInterval = 0.35f;

    private AsyncOperation _sceneOperation;

    private float _dotTimer;
    private int _dotCount = 1;
    private bool _isLoadingTextRunning;

    private const string GameplaySceneStr =
        "GameplayScene";

    private void Start()
    {
        progress.fillAmount = 0f;

        UpdateLoadingText();

        _isLoadingTextRunning = true;

        ATTrackingController.RequestTracking(() =>
        {
            StartLoading();
        });
    }

    private void Update()
    {
        if (!_isLoadingTextRunning)
            return;

        _dotTimer += Time.unscaledDeltaTime;

        if (_dotTimer < dotInterval)
            return;

        _dotTimer = 0f;

        _dotCount++;

        if (_dotCount > 3)
        {
            _dotCount = 1;
        }

        UpdateLoadingText();
    }

    private void UpdateLoadingText()
    {
        if (txtLoading == null)
            return;

        txtLoading.text =
            "Loading" + new string('.', _dotCount);
    }

    private void StartLoading()
    {
        string nextScene = GetNextScene();

        _sceneOperation =
            SceneManager.LoadSceneAsync(
                nextScene
            );

        _sceneOperation.allowSceneActivation = false;

        Tween.UIFillAmount(
            progress,
            1f,
            timeLoading
        )
        .OnUpdate<TextMeshProUGUI>(
            txtFill,
            (a, b) =>
            {
                int percentage =
                    Mathf.RoundToInt(
                        progress.fillAmount * 100f
                    );

                txtFill.text =
                    $"{percentage}%";
            }
        )
        .OnComplete(() =>
        {
            progress.fillAmount = 1f;
            txtFill.text = "100%";

            StartSceneTransition();
        });
    }

    private string GetNextScene()
    {
#if UNITY_EDITOR
        if (UnityEditor.SessionState.GetInt(
                Level.EditorPlayLevelKey,
                0
            ) > 0)
        {
            return GameplaySceneStr;
        }
#endif

        return GameplaySceneStr;
    }

    private void StartSceneTransition()
    {
        // The loading screen is already visible. Activate the preloaded scene
        // immediately instead of waiting for an extra TransitionManager cover.
        ActivateNextScene();
    }

    private void ActivateNextScene()
    {
        if (_sceneOperation == null)
            return;

        _isLoadingTextRunning = false;
        _sceneOperation.allowSceneActivation = true;
    }
}
