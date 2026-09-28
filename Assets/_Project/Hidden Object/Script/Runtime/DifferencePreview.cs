using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MagicSoft.Differences
{
    // Used only by the standalone preview scene, without the game's persistent managers.
    public sealed class DifferencePreview : MonoBehaviour
    {
        [SerializeField] private DifferenceLevelController board;
        [SerializeField] private DifferenceWorldBoard worldBoard;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button hintButton;
        [SerializeField] private Button replayButton;

        private void OnEnable()
        {
            board.ProgressChanged += Refresh;
            board.MistakesChanged += Refresh;
            board.Completed += Refresh;
            board.Failed += Refresh;
            hintButton.onClick.AddListener(board.RequestHint);
            replayButton.onClick.AddListener(Replay);
        }

        private void Start() { worldBoard.BindCamera(worldCamera); Replay(); }
        private void Replay() { board.BeginLevel(); Refresh(); }
        private void Refresh()
        {
            status.text = $"FIND THE DIFFERENCES   {board.FoundCount}/{board.TotalCount}\n" +
                $"Chances: {board.RemainingMistakes}/{board.MaxMistakes}   {board.State}";
        }

        private void OnDisable()
        {
            board.ProgressChanged -= Refresh;
            board.MistakesChanged -= Refresh;
            board.Completed -= Refresh;
            board.Failed -= Refresh;
            hintButton.onClick.RemoveListener(board.RequestHint);
            replayButton.onClick.RemoveListener(Replay);
        }
    }
}
