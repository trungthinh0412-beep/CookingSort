using System;
using MagicSoft.Differences;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Level : MonoBehaviour
{
    [SerializeField] private DifferenceLevelController differenceController;
    [SerializeField] private DifferenceWorldBoard worldBoard;
    public DifferenceLevelController Differences => differenceController;
    public event Action OnBoardChanged;
    private Action completed;
    private Action failed;

    private void Awake()
    {
        if (differenceController == null)
        {
            Debug.LogError($"[Level] {name}: assign Difference Controller in the prefab.", this);
            return;
        }
        differenceController.ProgressChanged += HandleBoardChanged;
        differenceController.MistakesChanged += HandleBoardChanged;
        differenceController.Completed += HandleCompleted;
        differenceController.Failed += HandleFailed;
    }

    public bool BeginLevel(Action onCompleted, Action onFailed)
    {
        completed = onCompleted;
        failed = onFailed;
        return differenceController != null && differenceController.BeginLevel();
    }

    public void SetPaused(bool paused) => differenceController?.SetPaused(paused);
    public void BindWorldCamera(Camera camera) => worldBoard?.BindCamera(camera);
    public void SetVisible(bool visible) => worldBoard?.SetVisible(visible);
    public void RequestHint() => differenceController?.RequestHint();

    // Legacy booster buttons are hidden for this gameplay.
    public void ActivateBooster(BoosterType boosterType) { }
    private void HandleBoardChanged() => OnBoardChanged?.Invoke();
    private void HandleCompleted() => completed?.Invoke();
    private void HandleFailed() => failed?.Invoke();

    private void OnDestroy()
    {
        if (differenceController == null) return;
        differenceController.ProgressChanged -= HandleBoardChanged;
        differenceController.MistakesChanged -= HandleBoardChanged;
        differenceController.Completed -= HandleCompleted;
        differenceController.Failed -= HandleFailed;
        completed = null;
        failed = null;
    }
}
