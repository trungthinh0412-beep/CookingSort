using System.Collections;
using UnityEngine;

public static class DarkKingCardTutorialState
{
    private const string SeenKey = "Tutorial.DarkKingCard.Level26.Seen";

    public static bool HasBeenSeen => PlayerPrefs.GetInt(SeenKey, 0) != 0;

    public static void MarkAsSeen()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
    }

    public static void ResetSeenState()
    {
        PlayerPrefs.DeleteKey(SeenKey);
        PlayerPrefs.Save();
    }
}

public sealed class Level26DarkKingCardTutorial : MonoBehaviour
{
    [SerializeField] private BonusTrayTutorialView tutorialPrefab;

    private Level _level;
    private BonusTrayTutorialView _activeTutorial;
    private Coroutine _showRoutine;

    private void OnEnable()
    {
        _level = GetComponentInParent<Level>();
        _showRoutine = StartCoroutine(ShowWhenReady());
    }

    private IEnumerator ShowWhenReady()
    {
        if (tutorialPrefab == null || _level == null ||
            Data.PlayerData == null ||
            Data.PlayerData.CurrentLevelIndex != 26 ||
            DarkKingCardTutorialState.HasBeenSeen)
        {
            yield break;
        }

        while (GameManager.Instance == null ||
               GameManager.Instance.gameState != GameState.PlayingGame ||
               GameManager.Instance.levelController == null ||
               GameManager.Instance.levelController.currentLevel != _level ||
               (TransitionManager.Instance != null && TransitionManager.Instance.IsPlaying) ||
               _level.IsTableEntrancePlaying ||
               (_level.InitialBoardDealAnimator != null &&
                _level.InitialBoardDealAnimator.IsBusy))
        {
            yield return null;
        }

        yield return null;
        _activeTutorial = Instantiate(tutorialPrefab);
        _activeTutorial.Dismissed += MarkSeen;
        _showRoutine = null;
    }

    private void MarkSeen()
    {
        DarkKingCardTutorialState.MarkAsSeen();
        _activeTutorial = null;
    }

    private void OnDisable()
    {
        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }

        if (_activeTutorial != null)
        {
            _activeTutorial.Dismissed -= MarkSeen;
            Destroy(_activeTutorial.gameObject);
            _activeTutorial = null;
        }
    }
}
