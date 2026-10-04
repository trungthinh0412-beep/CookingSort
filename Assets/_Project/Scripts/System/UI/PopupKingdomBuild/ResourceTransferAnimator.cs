using System;
using System.Collections;
using System.Collections.Generic;
using Lean.Pool;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ResourceTransferAnimator : MonoBehaviour
{
    [Header("Visual Gem Pool")]
    [SerializeField] private GemFlyVisual gemFlyVisualPrefab;
    [SerializeField] [Range(5, 10)] private int maxVisualGemCount = 10;

    [Header("Transfer Timing")]
    [SerializeField] [Min(0f)] private float startDelay = .02f;
    [Tooltip("Delay between icons. Icons follow each other; they do not wait for the previous arrival.")]
    [SerializeField] [Range(.02f, .2f)] private float spawnInterval = .08f;
    [SerializeField] [Range(.2f, 1f)] private float flightDuration = .55f;

    [Header("Half-Parabola Curve")]
    [Tooltip("0 = straight line, 1 = exact half-parabola with the source as its apex. The path remains one-way throughout this range.")]
    [SerializeField] [Range(0f, 1f)] private float curveStrength = 1f;
    [Tooltip("Position of the bend control point from Start (0) to Target (1). Above 0.5 stays flatter longer, then curves more sharply into the target.")]
    [SerializeField] [Range(.05f, .95f)] private float curveBendPosition = .65f;

    [Header("Visual Scale")]
    [SerializeField] [Range(.35f, 1.1f)] private float visualScale = .72f;
    [Tooltip("Gem starts small and grows while travelling toward the Build Button.")]
    [SerializeField] [Range(.35f, 1f)] private float startScaleMultiplier = .55f;
    [SerializeField] [Range(1f, 1.5f)] private float arrivalScaleMultiplier = 1.3f;
    [SerializeField] [Range(.7f, 1f)] private float targetShrinkMultiplier = .88f;
    [SerializeField] [Min(0f)] private float finalArrivalPause = .04f;
    [SerializeField] private Color gemTint = new Color(1f, .92f, .55f, 1f);
    [SerializeField] private bool useUnscaledTime = true;

    private readonly List<GemFlyVisual> activeVisuals =
        new List<GemFlyVisual>();

    private Coroutine transferRoutine;
    private int transferVersion;

    public void PlayTransfer(
        RectTransform resourceIcon,
        RectTransform targetButton,
        RectTransform targetGemIcon,
        TMP_Text requirementText,
        int amount,
        Action onComplete,
        Action<int> onGemArrived = null
    )
    {
        StopTransfer();

        int safeAmount = Mathf.Max(0, amount);

        if (requirementText != null)
            requirementText.text = safeAmount.ToString();

        if (safeAmount <= 0)
        {
            SetRequirementText(requirementText, 0);
            onComplete?.Invoke();
            return;
        }

        RectTransform destination = targetGemIcon != null
            ? targetGemIcon
            : targetButton;

        if (resourceIcon == null || destination == null ||
            gemFlyVisualPrefab == null)
        {
            // A missing visual reference must never block the gameplay flow.
            SetRequirementText(requirementText, 0);
            onComplete?.Invoke();
            return;
        }

        Canvas sourceCanvas = resourceIcon.GetComponentInParent<Canvas>();
        Canvas destinationCanvas = destination.GetComponentInParent<Canvas>();
        Canvas animationCanvas = destinationCanvas != null
            ? destinationCanvas
            : GetComponentInParent<Canvas>();

        RectTransform animationRoot = animationCanvas != null
            ? animationCanvas.transform as RectTransform
            : transform as RectTransform;

        if (animationRoot == null)
        {
            SetRequirementText(requirementText, 0);
            onComplete?.Invoke();
            return;
        }

        Vector2 startScreen = GetScreenPoint(resourceIcon, sourceCanvas);
        Vector2 endScreen = GetScreenPoint(destination, destinationCanvas);
        Vector2 visualSize = GetVisualSize(
            resourceIcon,
            sourceCanvas,
            animationRoot,
            animationCanvas
        );
        Sprite sourceSprite = GetSprite(resourceIcon);
        int visualGemCount = CalculateVisualGemCount(safeAmount);
        int version = transferVersion;

        transferRoutine = StartCoroutine(TransferRoutine(
            version,
            animationRoot,
            animationCanvas,
            sourceSprite,
            visualSize,
            startScreen,
            endScreen,
            requirementText,
            safeAmount,
            visualGemCount,
            onComplete,
            onGemArrived
        ));
    }

    public void StopTransfer()
    {
        transferVersion++;

        if (transferRoutine != null)
            StopCoroutine(transferRoutine);

        transferRoutine = null;
        DespawnAllVisuals();
    }

    private void OnDisable()
    {
        StopTransfer();
    }

    private IEnumerator TransferRoutine(
        int version,
        RectTransform animationRoot,
        Canvas animationCanvas,
        Sprite sourceSprite,
        Vector2 visualSize,
        Vector2 startScreen,
        Vector2 endScreen,
        TMP_Text requirementText,
        int amount,
        int visualGemCount,
        Action onComplete,
        Action<int> onGemArrived
    )
    {
        List<VisualGemFlight> flights = new List<VisualGemFlight>(
            visualGemCount
        );

        float elapsed = 0f;
        int nextGemIndex = 0;
        int arrivedGemCount = 0;
        float safeSpawnInterval = Mathf.Max(.001f, spawnInterval);

        while (nextGemIndex < visualGemCount || flights.Count > 0)
        {
            if (!IsTransferValid(version, animationRoot))
            {
                CancelRunningTransfer();
                yield break;
            }

            while (nextGemIndex < visualGemCount &&
                   elapsed >= startDelay + safeSpawnInterval * nextGemIndex)
            {
                GemFlyVisual visual = SpawnVisual(
                    animationRoot,
                    animationCanvas,
                    sourceSprite,
                    visualSize,
                    startScreen
                );

                if (visual != null)
                {
                    flights.Add(new VisualGemFlight
                    {
                        Visual = visual,
                        StartScreen = startScreen,
                        EndScreen = endScreen,
                        ControlScreen = GetHalfParabolaControlPoint(
                            startScreen,
                            endScreen,
                            curveStrength,
                            curveBendPosition
                        ),
                        Rotation = GetRotation(nextGemIndex),
                        Duration = Mathf.Max(.01f, flightDuration),
                        // Preserve stagger even when a slow frame spawns several icons.
                        Elapsed = Mathf.Max(0f, elapsed -
                            (startDelay + safeSpawnInterval * nextGemIndex))
                    });
                }

                nextGemIndex++;
            }

            float deltaTime = GetDeltaTime();

            // Process oldest first so same-frame arrivals keep their launch order.
            for (int i = 0; i < flights.Count;)
            {
                if (!IsTransferValid(version, animationRoot))
                    yield break;

                VisualGemFlight flight = flights[i];

                if (flight.Visual == null)
                {
                    flights.RemoveAt(i);
                    arrivedGemCount++;
                    SetRequirementValue(
                        requirementText,
                        amount,
                        arrivedGemCount,
                        visualGemCount
                    );
                    onGemArrived?.Invoke(arrivedGemCount);
                    continue;
                }

                flight.Elapsed += deltaTime;
                float progress = Mathf.Clamp01(
                    flight.Elapsed / flight.Duration
                );
                float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                Vector2 screenPosition = EvaluateBezier(
                    flight.StartScreen,
                    flight.ControlScreen,
                    flight.EndScreen,
                    easedProgress
                );
                Vector3 worldPosition = ScreenToWorldPoint(
                    animationRoot,
                    animationCanvas,
                    screenPosition
                );
                float pop = Mathf.Sin(progress * Mathf.PI) * .06f;
                float scaleMultiplier = Mathf.Lerp(
                    Mathf.Clamp(startScaleMultiplier, .35f, 1f),
                    Mathf.Clamp(arrivalScaleMultiplier, 1f, 1.5f),
                    easedProgress
                );
                float targetShrink = Mathf.Lerp(
                    1f,
                    Mathf.Clamp(targetShrinkMultiplier, .7f, 1f),
                    Mathf.InverseLerp(.82f, 1f, progress)
                );
                float scale = visualScale *
                              (scaleMultiplier + pop) *
                              targetShrink;

                flight.Visual.SetMotion(
                    worldPosition,
                    Mathf.Lerp(flight.Rotation, -flight.Rotation, progress),
                    scale
                );

                if (progress < 1f)
                {
                    flights[i] = flight;
                    i++;
                    continue;
                }

                DespawnVisual(flight.Visual);
                flights.RemoveAt(i);
                arrivedGemCount++;
                SetRequirementValue(
                    requirementText,
                    amount,
                    arrivedGemCount,
                    visualGemCount
                );
                onGemArrived?.Invoke(arrivedGemCount);
            }

            elapsed += deltaTime;
            yield return null;
        }

        SetRequirementText(requirementText, 0);
        yield return WaitForSeconds(finalArrivalPause);

        if (!IsTransferValid(version, animationRoot))
        {
            CancelRunningTransfer();
            yield break;
        }

        transferRoutine = null;
        onComplete?.Invoke();
    }

    private GemFlyVisual SpawnVisual(
        RectTransform animationRoot,
        Canvas animationCanvas,
        Sprite sourceSprite,
        Vector2 visualSize,
        Vector2 startScreen
    )
    {
        GemFlyVisual visual = LeanPool.Spawn(
            gemFlyVisualPrefab,
            animationRoot
        );

        if (visual == null)
            return null;

        activeVisuals.Add(visual);
        float initialScale = visualScale * Mathf.Clamp(
            startScaleMultiplier,
            .35f,
            1f
        );
        visual.Prepare(sourceSprite, gemTint, visualSize, initialScale);
        visual.SetMotion(
            ScreenToWorldPoint(animationRoot, animationCanvas, startScreen),
            0f,
            initialScale
        );
        return visual;
    }

    private void DespawnVisual(GemFlyVisual visual)
    {
        if (visual == null)
            return;

        activeVisuals.Remove(visual);
        visual.ResetVisual();
        LeanPool.Despawn(visual);
    }

    private void DespawnAllVisuals()
    {
        for (int i = activeVisuals.Count - 1; i >= 0; i--)
        {
            GemFlyVisual visual = activeVisuals[i];

            if (visual != null)
            {
                visual.ResetVisual();
                LeanPool.Despawn(visual);
            }
        }

        activeVisuals.Clear();
    }

    private void CancelRunningTransfer()
    {
        transferVersion++;
        transferRoutine = null;
        DespawnAllVisuals();
    }

    private bool IsTransferValid(int version, RectTransform animationRoot)
    {
        return version == transferVersion &&
               isActiveAndEnabled &&
               animationRoot != null &&
               animationRoot.gameObject.activeInHierarchy;
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private IEnumerator WaitForSeconds(float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += GetDeltaTime();
            yield return null;
        }
    }

    private int CalculateVisualGemCount(int amount)
    {
        if (amount <= 5)
            return amount;

        int calculatedCount = 3 + Mathf.CeilToInt(Mathf.Sqrt(amount));
        return Mathf.Clamp(calculatedCount, 5, Mathf.Max(5, maxVisualGemCount));
    }

    private static void SetRequirementValue(
        TMP_Text requirementText,
        int amount,
        int arrivedGemCount,
        int visualGemCount
    )
    {
        if (requirementText == null)
            return;

        float progress = visualGemCount <= 0
            ? 1f
            : Mathf.Clamp01(arrivedGemCount / (float)visualGemCount);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        int remaining = arrivedGemCount >= visualGemCount
            ? 0
            : Mathf.Max(
                1,
                Mathf.CeilToInt(Mathf.Lerp(amount, 0f, easedProgress))
            );
        requirementText.text = Mathf.Max(0, remaining).ToString();
    }

    private static void SetRequirementText(TMP_Text requirementText, int value)
    {
        if (requirementText != null)
            requirementText.text = Mathf.Max(0, value).ToString();
    }

    private static Vector2 GetHalfParabolaControlPoint(
        Vector2 start,
        Vector2 end,
        float strength,
        float bendPosition
    )
    {
        // Keeping the control point between the endpoint X values makes horizontal
        // movement monotonic: it cannot overshoot and reverse. Control Y moves
        // toward source Y as strength approaches 1, producing a sharper one-sided
        // parabolic branch. Bend position shifts the turn without creating a full arc.
        float midpointY = (start.y + end.y) * .5f;
        return new Vector2(
            Mathf.Lerp(start.x, end.x, Mathf.Clamp(bendPosition, .05f, .95f)),
            Mathf.Lerp(midpointY, start.y, Mathf.Clamp01(strength))
        );
    }

    private static float GetRotation(int index)
    {
        int cycle = index % 4;
        return cycle switch
        {
            0 => -9f,
            1 => 7f,
            2 => -5f,
            _ => 10f
        };
    }

    private static Vector2 EvaluateBezier(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        float progress
    )
    {
        float inverse = 1f - progress;
        return inverse * inverse * start +
               2f * inverse * progress * control +
               progress * progress * end;
    }

    private static Vector2 GetScreenPoint(RectTransform rect, Canvas canvas)
    {
        Camera camera = GetCanvasCamera(canvas);
        Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
        return RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
    }

    private static Vector3 ScreenToWorldPoint(
        RectTransform root,
        Canvas canvas,
        Vector2 screenPoint
    )
    {
        Camera camera = GetCanvasCamera(canvas);

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                root,
                screenPoint,
                camera,
                out Vector3 worldPoint
            ))
        {
            return worldPoint;
        }

        return root.position;
    }

    private static Camera GetCanvasCamera(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private static Vector2 GetVisualSize(
        RectTransform source,
        Canvas sourceCanvas,
        RectTransform animationRoot,
        Canvas animationCanvas
    )
    {
        Vector3[] corners = new Vector3[4];
        source.GetWorldCorners(corners);

        Camera sourceCamera = GetCanvasCamera(sourceCanvas);
        Vector2 bottomLeftScreen = RectTransformUtility.WorldToScreenPoint(
            sourceCamera,
            corners[0]
        );
        Vector2 topLeftScreen = RectTransformUtility.WorldToScreenPoint(
            sourceCamera,
            corners[1]
        );
        Vector2 bottomRightScreen = RectTransformUtility.WorldToScreenPoint(
            sourceCamera,
            corners[3]
        );
        Vector3 bottomLeft = animationRoot.InverseTransformPoint(
            ScreenToWorldPoint(animationRoot, animationCanvas, bottomLeftScreen)
        );
        Vector3 topLeft = animationRoot.InverseTransformPoint(
            ScreenToWorldPoint(animationRoot, animationCanvas, topLeftScreen)
        );
        Vector3 bottomRight = animationRoot.InverseTransformPoint(
            ScreenToWorldPoint(animationRoot, animationCanvas, bottomRightScreen)
        );
        float width = Vector3.Distance(bottomLeft, bottomRight);
        float height = Vector3.Distance(bottomLeft, topLeft);
        // Keep the size in the destination canvas' local units. This makes
        // the same prefab work on overlay, camera and world-space canvases.
        float size = Mathf.Max(.0001f, Mathf.Min(width, height) * .72f);

        return new Vector2(size, size);
    }

    private static Sprite GetSprite(RectTransform source)
    {
        Image image = source.GetComponentInChildren<Image>(true);

        if (image != null && image.sprite != null)
            return image.sprite;

        SpriteRenderer spriteRenderer =
            source.GetComponentInChildren<SpriteRenderer>(true);

        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    private sealed class VisualGemFlight
    {
        public GemFlyVisual Visual;
        public Vector2 StartScreen;
        public Vector2 ControlScreen;
        public Vector2 EndScreen;
        public float Rotation;
        public float Duration;
        public float Elapsed;
    }
}
