using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public static class StoryIntroState
{
    // Change v1 to v2 only when a new intro must be shown to every player.
    private const string WatchedKey = "StoryIntro.Watched.v1";

    public static bool HasBeenWatched =>
        PlayerPrefs.GetInt(WatchedKey, 0) == 1;

    public static void MarkAsWatched()
    {
        PlayerPrefs.SetInt(WatchedKey, 1);
        PlayerPrefs.Save();
    }

    public static void ResetWatchedState()
    {
        PlayerPrefs.DeleteKey(WatchedKey);
        PlayerPrefs.Save();
    }
}

public class StoryIntroController : MonoBehaviour
{
    private const string GameplaySceneName = "GameplayScene";

    [Header("Story Video")]
    [Tooltip("Optional. If empty, the video is read from StreamingAssets.")]
    [SerializeField] private VideoClip videoClip;

    [SerializeField] private string streamingAssetsFileName =
        "StoryIntro.mp4";

    [SerializeField] private float prepareTimeout = 15f;

    private VideoPlayer _videoPlayer;
    private AsyncOperation _gameplaySceneOperation;
    private bool _videoPrepared;
    private bool _isFinishing;

    private void Start()
    {
        PreloadGameplayScene();

        if (StoryIntroState.HasBeenWatched)
        {
            StartCoroutine(FinishIntroRoutine(false));
            return;
        }

        CreateAndPrepareVideoPlayer();
    }

    private void PreloadGameplayScene()
    {
        _gameplaySceneOperation =
            SceneManager.LoadSceneAsync(GameplaySceneName);

        if (_gameplaySceneOperation != null)
        {
            _gameplaySceneOperation.allowSceneActivation = false;
        }
    }

    private void CreateAndPrepareVideoPlayer()
    {
        Camera introCamera = CreateIntroCamera();

        _videoPlayer =
            gameObject.AddComponent<VideoPlayer>();

        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = false;
        _videoPlayer.waitForFirstFrame = true;
        _videoPlayer.skipOnDrop = true;
        _videoPlayer.renderMode =
            VideoRenderMode.CameraNearPlane;
        _videoPlayer.targetCamera = introCamera;
        _videoPlayer.aspectRatio =
            VideoAspectRatio.FitInside;
        _videoPlayer.audioOutputMode =
            VideoAudioOutputMode.Direct;

        if (videoClip != null)
        {
            _videoPlayer.source =
                VideoSource.VideoClip;
            _videoPlayer.clip = videoClip;
        }
        else
        {
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = BuildStreamingAssetsUrl();
        }

        _videoPlayer.prepareCompleted +=
            HandlePrepareCompleted;
        _videoPlayer.loopPointReached +=
            HandleVideoFinished;
        _videoPlayer.errorReceived +=
            HandleVideoError;

        _videoPlayer.Prepare();
        StartCoroutine(WaitForVideoAndTransition());
    }

    private Camera CreateIntroCamera()
    {
        GameObject cameraObject =
            new GameObject("StoryIntroCamera");

        Camera introCamera =
            cameraObject.AddComponent<Camera>();

        cameraObject.AddComponent<AudioListener>();

        introCamera.clearFlags =
            CameraClearFlags.SolidColor;
        introCamera.backgroundColor = Color.black;

        return introCamera;
    }

    private string BuildStreamingAssetsUrl()
    {
        string root =
            Application.streamingAssetsPath
                .TrimEnd('/', '\\');

        return root + "/" +
               streamingAssetsFileName.TrimStart('/', '\\');
    }

    private void HandlePrepareCompleted(
        VideoPlayer source
    )
    {
        _videoPrepared = true;
    }

    private IEnumerator WaitForVideoAndTransition()
    {
        float elapsed = 0f;

        while (!_videoPrepared && !_isFinishing)
        {
            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= prepareTimeout)
            {
                Debug.LogError(
                    "[StoryIntro] Video preparation timed out. " +
                    "Continuing to gameplay."
                );

                StartCoroutine(
                    FinishIntroRoutine(false)
                );

                yield break;
            }

            yield return null;
        }

        while (!_isFinishing &&
               TransitionManager.Instance != null &&
               TransitionManager.Instance.IsPlaying)
        {
            yield return null;
        }

        if (!_isFinishing && _videoPlayer != null)
        {
            _videoPlayer.Play();
        }
    }

    private void HandleVideoFinished(
        VideoPlayer source
    )
    {
        StartCoroutine(FinishIntroRoutine(true));
    }

    private void HandleVideoError(
        VideoPlayer source,
        string message
    )
    {
        Debug.LogError(
            "[StoryIntro] Cannot play story video: " +
            message
        );

        StartCoroutine(FinishIntroRoutine(false));
    }

    // Can be wired to a UI button later if a Skip button is desired.
    public void SkipIntro()
    {
        StartCoroutine(FinishIntroRoutine(true));
    }

    private IEnumerator FinishIntroRoutine(
        bool markAsWatched
    )
    {
        if (_isFinishing)
            yield break;

        _isFinishing = true;

        if (markAsWatched)
        {
            StoryIntroState.MarkAsWatched();
        }

        if (_videoPlayer != null)
        {
            _videoPlayer.Stop();
        }

        while (TransitionManager.Instance != null &&
               TransitionManager.Instance.IsPlaying)
        {
            yield return null;
        }

        if (TransitionManager.Instance == null)
        {
            ActivateGameplayScene();
            yield break;
        }

        TransitionManager.Instance.PlayCover(
            ActivateGameplayScene
        );
    }

    private void ActivateGameplayScene()
    {
        if (_gameplaySceneOperation != null)
        {
            _gameplaySceneOperation.allowSceneActivation =
                true;
            return;
        }

        SceneManager.LoadScene(GameplaySceneName);
    }

    private void OnDestroy()
    {
        if (_videoPlayer == null)
            return;

        _videoPlayer.prepareCompleted -=
            HandlePrepareCompleted;
        _videoPlayer.loopPointReached -=
            HandleVideoFinished;
        _videoPlayer.errorReceived -=
            HandleVideoError;
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Story Intro Watched State")]
    private void ResetStoryIntroWatchedState()
    {
        StoryIntroState.ResetWatchedState();
        Debug.Log(
            "[StoryIntro] Watched state reset. " +
            "The intro will play on the next launch."
        );
    }
#endif
}
