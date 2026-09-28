using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicSoft.Differences
{
    public enum DifferenceLevelState { Ready, Playing, Won, Lost }

    [DisallowMultipleComponent]
    public sealed class DifferenceLevelController : MonoBehaviour
    {
        [Header("Pictures (explicit references)")]
        [SerializeField] private DifferencePanelView panelA;
        [SerializeField] private DifferencePanelView panelB;
        [Header("Drag matching spots from both pictures into each pair")]
        [SerializeField] private List<DifferencePair> differences = new List<DifferencePair>();
        [Header("Rules")]
        [Tooltip("0 = unlimited mistakes.")]
        [SerializeField, Min(0)] private int maxMistakes = 3;

        private readonly HashSet<int> found = new HashSet<int>();
        private bool paused;
        public DifferenceLevelState State { get; private set; }
        public int FoundCount => found.Count;
        public int TotalCount => differences.Count;
        public int MistakeCount { get; private set; }
        public int MaxMistakes => maxMistakes;
        public int RemainingMistakes => Mathf.Max(0, maxMistakes - MistakeCount);
        public bool CanInteract => isActiveAndEnabled && !paused && Time.timeScale > 0f &&
                                   State == DifferenceLevelState.Playing;
        public event Action ProgressChanged;
        public event Action MistakesChanged;
        public event Action Completed;
        public event Action Failed;

        private void OnEnable()
        {
            if (panelA != null) panelA.PictureClicked += HandlePictureClick;
            if (panelB != null) panelB.PictureClicked += HandlePictureClick;
        }

        private void OnDisable()
        {
            if (panelA != null) panelA.PictureClicked -= HandlePictureClick;
            if (panelB != null) panelB.PictureClicked -= HandlePictureClick;
        }

        public bool BeginLevel()
        {
            State = DifferenceLevelState.Ready;
            if (!TryValidate(out string error))
            {
                Debug.LogError($"[Differences] {name}: {error}", this);
                return false;
            }
            ResetLevel();
            paused = false;
            State = DifferenceLevelState.Playing;
            return true;
        }

        public void ResetLevel()
        {
            State = DifferenceLevelState.Ready;
            found.Clear();
            MistakeCount = 0;
            foreach (var pair in differences)
            {
                if (pair == null) continue;
                pair.SpotA?.ResetView();
                pair.SpotB?.ResetView();
            }
            ProgressChanged?.Invoke();
            MistakesChanged?.Invoke();
        }

        public void SetPaused(bool value) => paused = value;

        public void HandlePictureClick(DifferencePanelView panel, Vector2 position, Camera eventCamera)
        {
            if (!CanInteract || (panel != panelA && panel != panelB)) return;
            // Ignore clicks outside the actual picture, including letterboxing.
            if (!panel.ContainsScreenPoint(position, eventCamera) ||
                !panel.TryScreenPoint(position, eventCamera, out var worldPoint)) return;

            // Ignore already-found regions first, including accidentally overlapping authoring areas.
            foreach (int index in found)
            {
                var spot = panel == panelA ? differences[index].SpotA : differences[index].SpotB;
                if (spot.ContainsWorldPoint(worldPoint)) return;
            }
            for (int i = 0; i < differences.Count; i++)
            {
                var pair = differences[i];
                var spot = panel == panelA ? pair.SpotA : pair.SpotB;
                if (!spot.ContainsWorldPoint(worldPoint)) continue;
                found.Add(i);
                pair.SpotA.SetFound(true);
                pair.SpotB.SetFound(true);
                bool won = found.Count == differences.Count;
                if (won) State = DifferenceLevelState.Won;
                ProgressChanged?.Invoke();
                if (won) Completed?.Invoke();
                return;
            }
            MistakeCount++;
            bool lost = maxMistakes > 0 && MistakeCount >= maxMistakes;
            if (lost) State = DifferenceLevelState.Lost;
            MistakesChanged?.Invoke();
            if (lost) Failed?.Invoke();
        }

        public void RequestHint()
        {
            if (!CanInteract) return;
            for (int i = 0; i < differences.Count; i++)
            {
                if (found.Contains(i)) continue;
                differences[i].SpotA.ShowHint();
                differences[i].SpotB.ShowHint();
                return;
            }
        }

        public bool TryValidate(out string error)
        {
            if (panelA == null || panelB == null || panelA == panelB ||
                !panelA.IsConfigured || !panelB.IsConfigured)
            {
                error = "Assign two different panels with SpriteRenderer sprites and an enabled picture BoxCollider2D.";
                return false;
            }
            if (differences.Count == 0)
            {
                error = "Add at least one Difference Pair.";
                return false;
            }
            var ids = new HashSet<string>();
            var spots = new HashSet<DifferenceSpotView>();
            foreach (var pair in differences)
            {
                if (pair == null || string.IsNullOrWhiteSpace(pair.Id) || !ids.Add(pair.Id))
                {
                    error = "Every pair needs a unique, non-empty Id.";
                    return false;
                }
                if (!ValidateSpot(pair.SpotA, panelA, spots) || !ValidateSpot(pair.SpotB, panelB, spots))
                {
                    error = $"Pair '{pair.Id}': assign unique A/B spots directly under the correct Artwork, with Picture, Hit Area, marker sprite, and bounds inside (0..1).";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static bool ValidateSpot(DifferenceSpotView spot, DifferencePanelView panel,
            HashSet<DifferenceSpotView> used)
        {
            if (spot == null) return false;
            var region = spot.NormalizedBounds;
            if (region.width <= 0 || region.height <= 0 || region.xMin < -.001f || region.yMin < -.001f ||
                region.xMax > 1.001f || region.yMax > 1.001f) return false;
            return spot != null && used.Add(spot) && spot.HitArea != null &&
                spot.FoundMarker != null && spot.FoundMarker.sprite != null && spot.Picture == panel.Picture &&
                spot.transform.parent == panel.Picture.transform &&
                (spot.HitArea.transform == spot.transform || spot.HitArea.transform.IsChildOf(spot.transform)) &&
                spot.HitArea.transform.IsChildOf(panel.Picture.transform) &&
                spot.FoundMarker.transform.IsChildOf(spot.transform);
        }
    }
}
