using System;
using System.Collections.Generic;
using CustomTween;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BuildRevealController : MonoBehaviour
{
    // Drives a per-renderer reveal and the construction-line VFX together.
    private static readonly int BuildProgressId = Shader.PropertyToID("_BuildProgress");
    private static readonly int RevealTopYId = Shader.PropertyToID("_RevealTopY");
    private static readonly int RevealBottomYId = Shader.PropertyToID("_RevealBottomY");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int EdgeGlowWidthId = Shader.PropertyToID("_EdgeGlowWidth");
    private static readonly int EdgeGlowColorId = Shader.PropertyToID("_EdgeGlowColor");
    private static readonly int EdgeGlowStrengthId = Shader.PropertyToID("_EdgeGlowStrength");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Reveal")]
    [SerializeField] private Material revealMaterial;
    [SerializeField] private BuildRevealVFX buildRevealVfxPrefab;
    [SerializeField] [Min(.01f)] private float buildDuration = .45f;
    [SerializeField] [Range(0f, .2f)] private float edgeSoftness = .018f;
    [SerializeField] [Range(0f, .5f)] private float edgeGlowWidth = .06f;
    [SerializeField] private Color edgeGlowColor = new Color(1f, .78f, .35f, 1f);
    [SerializeField] [Range(0f, 1f)] private float edgeGlowStrength = .22f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Lift and settle")]
    [Tooltip("Raises the object by this fraction of its height while it is being built.")]
    [SerializeField] [Range(0f, .15f)] private float objectLiftHeightRatio = .045f;
    [Tooltip("Small drop duration used when the completed object settles into place.")]
    [SerializeField] [Min(0f)] private float settleDuration = .14f;
    [Tooltip("Slightly undersizes the object at the start of the reveal.")]
    [SerializeField] [Range(.9f, 1f)] private float buildStartScale = .97f;
    [Tooltip("Small scale overshoot while the object settles into place.")]
    [SerializeField] [Range(0f, .15f)] private float settleScalePunch = .035f;

    [Header("Build VFX")]
    [SerializeField] [Min(0f)] private float dustAmount = 1f;
    [SerializeField] [Min(0f)] private float sparkleAmount = 1f;
    [Tooltip("World-space Y offset applied to the moving construction line.")]
    [SerializeField] private float fxVerticalOffset;
    [SerializeField] [Min(.01f)] private float fxWidthMultiplier = 1f;

    private Tween revealTween;
    private BuildTarget activeTarget;
    private BuildRevealVFX activeVfx;
    private BuildRevealShadow activeShadow;
    private Action activeCompleteCallback;
    private bool isCompleting;

    public float BuildDuration
    {
        get => buildDuration;
        set => buildDuration = Mathf.Max(.01f, value);
    }

    public float EdgeSoftness
    {
        get => edgeSoftness;
        set => edgeSoftness = Mathf.Max(0f, value);
    }

    public float EdgeGlowWidth
    {
        get => edgeGlowWidth;
        set => edgeGlowWidth = Mathf.Max(0f, value);
    }

    public float DustAmount
    {
        get => dustAmount;
        set => dustAmount = Mathf.Max(0f, value);
    }

    public float SparkleAmount
    {
        get => sparkleAmount;
        set => sparkleAmount = Mathf.Max(0f, value);
    }

    public float FXVerticalOffset
    {
        get => fxVerticalOffset;
        set => fxVerticalOffset = value;
    }

    public float FXWidthMultiplier
    {
        get => fxWidthMultiplier;
        set => fxWidthMultiplier = Mathf.Max(.01f, value);
    }

    public bool UseUnscaledTime
    {
        get => useUnscaledTime;
        set => useUnscaledTime = value;
    }

    public BuildRevealVFX BuildRevealVfxPrefab
    {
        get => buildRevealVfxPrefab;
        set => buildRevealVfxPrefab = value;
    }

    public Tween PlayBuild(Transform target, float duration)
    {
        return PlayBuild(target, duration, null);
    }

    public Tween PlayBuild(
        Transform target,
        float duration,
        Action onComplete
    )
    {
        StopBuild();

        if (target == null)
        {
            onComplete?.Invoke();
            return default;
        }

        BuildTarget targetBinding = CreateTargetBinding(target);

        if (!targetBinding.HasVisuals || !TryGetTargetBounds(targetBinding, out Bounds bounds))
        {
            onComplete?.Invoke();
            return default;
        }

        activeTarget = targetBinding;
        activeTarget.Bounds = bounds;
        activeTarget.RestPosition = target.position;
        activeTarget.RestScale = target.localScale;
        activeTarget.LiftedPosition = target.position + Vector3.up * Mathf.Max(
            .02f,
            bounds.size.y * objectLiftHeightRatio
        );
        activeTarget.IsLifted = activeTarget.LiftedPosition !=
                                activeTarget.RestPosition;
        activeCompleteCallback = onComplete;

        target.localScale = activeTarget.RestScale * buildStartScale;
        target.position = activeTarget.LiftedPosition;

        RefreshActiveBounds();

        ApplyRevealProgress(0f);
        activeShadow = BuildRevealShadow.Create(target, activeTarget.Bounds);
        activeVfx = SpawnBuildVfx(target, activeTarget.Bounds);

        float safeDuration = duration > 0f
            ? duration
            : buildDuration;

        safeDuration = Mathf.Max(0f, safeDuration);

        if (safeDuration <= 0f)
        {
            CompleteBuild();
            return default;
        }

        revealTween = Tween.Custom(
            0f,
            1f,
            safeDuration,
            SetRevealProgress,
            Ease.Linear,
            useUnscaledTime: useUnscaledTime
        );

        revealTween.OnComplete(CompleteBuild);
        return revealTween;
    }

    public Tween PlayBuild(SpriteRenderer target, float duration)
    {
        return PlayBuild(target != null ? target.transform : null, duration);
    }

    public void StopBuild()
    {
        if (revealTween.isAlive)
            revealTween.Stop();

        revealTween = default;
        activeCompleteCallback = null;
        isCompleting = false;

        if (activeTarget != null)
        {
            RestoreTargetPosition();
            RefreshActiveBounds();
            ApplyRevealProgress(1f);
        }

        if (activeVfx != null)
            activeVfx.StopAndDestroy();

        if (activeShadow != null)
            activeShadow.DestroyShadow();

        activeVfx = null;
        activeShadow = null;
        activeTarget = null;
    }

    private void OnDisable()
    {
        StopBuild();
    }

    private void CompleteBuild()
    {
        if (isCompleting || activeTarget == null)
            return;

        isCompleting = true;
        ApplyRevealScale(1f);
        RefreshActiveBounds();
        ApplyRevealProgress(1f);

        if (activeTarget.IsLifted && settleDuration > 0f)
        {
            StartTargetSettle();
            return;
        }

        FinishCompletedBuild();
    }

    private void StartTargetSettle()
    {
        Tween settleTween = Tween.Custom(
            0f,
            1f,
            settleDuration,
            SettleTarget,
            Ease.Linear,
            useUnscaledTime: useUnscaledTime
        );

        settleTween.OnComplete(FinishCompletedBuild);
        revealTween = settleTween;
    }

    private void SettleTarget(float progress)
    {
        if (activeTarget == null || activeTarget.Target == null)
            return;

        float easedProgress = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress), 2f);
        activeTarget.Target.position = Vector3.Lerp(
            activeTarget.LiftedPosition,
            activeTarget.RestPosition,
            easedProgress
        );

        float scalePunch = Mathf.Sin(easedProgress * Mathf.PI) *
                           Mathf.Max(0f, settleScalePunch);
        activeTarget.Target.localScale = activeTarget.RestScale *
                                         (1f + scalePunch);

        RefreshActiveBounds();

        ApplyRevealProgress(1f);
    }

    private void FinishCompletedBuild()
    {
        if (activeTarget == null)
            return;

        RestoreTargetPosition();

        if (TryGetTargetBounds(activeTarget, out Bounds completedBounds))
            activeTarget.Bounds = completedBounds;

        if (activeVfx != null)
            activeVfx.Complete(activeTarget.Bounds);

        if (activeShadow != null)
            activeShadow.DestroyShadow();

        activeShadow = null;

        Action completeCallback = activeCompleteCallback;

        revealTween = default;
        activeVfx = null;
        activeTarget = null;
        activeCompleteCallback = null;
        isCompleting = false;

        // The VFX owns its finish-burst cleanup, so it is intentionally not
        // destroyed here with the controller's active state.
        completeCallback?.Invoke();
    }

    private void RestoreTargetPosition()
    {
        if (activeTarget?.Target == null)
            return;

        if (activeTarget.IsLifted)
            activeTarget.Target.position = activeTarget.RestPosition;

        activeTarget.Target.localScale = activeTarget.RestScale;
    }

    private void SetRevealProgress(float progress)
    {
        if (activeTarget == null)
            return;

        ApplyRevealScale(progress);
        RefreshActiveBounds();
        ApplyRevealProgress(progress);
    }

    private void ApplyRevealScale(float progress)
    {
        if (activeTarget?.Target == null)
            return;

        float clampedProgress = Mathf.Clamp01(progress);
        float scaleMultiplier = Mathf.Lerp(
            Mathf.Clamp(buildStartScale, .9f, 1f),
            1f,
            clampedProgress
        );
        activeTarget.Target.localScale = activeTarget.RestScale *
                                         scaleMultiplier;
    }

    private void RefreshActiveBounds()
    {
        if (activeTarget != null &&
            TryGetTargetBounds(activeTarget, out Bounds refreshedBounds))
        {
            activeTarget.Bounds = refreshedBounds;
        }
    }

    private void ApplyRevealProgress(float progress)
    {
        if (activeTarget == null)
            return;

        float clampedProgress = Mathf.Clamp01(progress);
        Bounds bounds = activeTarget.Bounds;
        float height = Mathf.Max(.0001f, bounds.size.y);
        float softnessWorld = height * edgeSoftness;
        float glowWidthWorld = height * edgeGlowWidth;

        for (int i = 0; i < activeTarget.Graphics.Count; i++)
        {
            GraphicBinding graphicBinding = activeTarget.Graphics[i];

            if (graphicBinding.Graphic == null || graphicBinding.Material == null)
                continue;

            SetMaterialProperties(
                graphicBinding.Material,
                clampedProgress,
                bounds.max.y,
                bounds.min.y,
                softnessWorld,
                glowWidthWorld
            );
            graphicBinding.Graphic.SetMaterialDirty();
        }

        for (int i = 0; i < activeTarget.Renderers.Count; i++)
        {
            RendererBinding rendererBinding = activeTarget.Renderers[i];
            Renderer renderer = rendererBinding.Renderer;

            if (renderer == null)
                continue;

            MaterialPropertyBlock block = rendererBinding.PropertyBlock;
            block.SetFloat(BuildProgressId, clampedProgress);
            block.SetFloat(RevealTopYId, bounds.max.y);
            block.SetFloat(RevealBottomYId, bounds.min.y);
            block.SetFloat(EdgeSoftnessId, softnessWorld);
            block.SetFloat(EdgeGlowWidthId, glowWidthWorld);
            block.SetColor(EdgeGlowColorId, edgeGlowColor);
            block.SetFloat(EdgeGlowStrengthId, edgeGlowStrength);

            if (rendererBinding.MaterialCount <= 1)
            {
                renderer.SetPropertyBlock(block);
                continue;
            }

            for (int materialIndex = 0;
                 materialIndex < rendererBinding.MaterialCount;
                 materialIndex++)
            {
                renderer.SetPropertyBlock(block, materialIndex);
            }
        }

        if (activeVfx != null)
            activeVfx.SetRevealEdge(bounds, clampedProgress);
    }

    private BuildRevealVFX SpawnBuildVfx(Transform target, Bounds bounds)
    {
        BuildRevealVFX vfx = buildRevealVfxPrefab != null
            ? Instantiate(buildRevealVfxPrefab, target.parent)
            : CreateRuntimeVfx(target.parent);

        if (vfx == null)
            return null;

        SetLayerRecursively(vfx.transform, target.gameObject.layer);
        vfx.Play(
            target,
            bounds,
            dustAmount,
            sparkleAmount,
            fxVerticalOffset,
            fxWidthMultiplier
        );
        return vfx;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
            return;

        // Keep world VFX visible when a target uses a non-default camera layer.
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    private static BuildRevealVFX CreateRuntimeVfx(Transform parent)
    {
        GameObject vfxObject = new GameObject("BuildRevealVFX_Runtime");
        vfxObject.transform.SetParent(parent, false);
        return vfxObject.AddComponent<BuildRevealVFX>();
    }

    private BuildTarget CreateTargetBinding(Transform target)
    {
        BuildTarget targetBinding = new BuildTarget
        {
            Target = target
        };

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            RendererBinding rendererBinding = PrepareRenderer(renderer);

            if (rendererBinding != null)
                targetBinding.Renderers.Add(rendererBinding);
        }

        Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null)
                continue;

            GraphicBinding graphicBinding = PrepareGraphic(graphic);

            if (graphicBinding != null)
                targetBinding.Graphics.Add(graphicBinding);
        }

        return targetBinding;
    }

    private RendererBinding PrepareRenderer(Renderer renderer)
    {
        Material runtimeRevealMaterial = ResolveRevealMaterial();

        if (runtimeRevealMaterial == null)
            return null;

        Material[] sourceMaterials = renderer.sharedMaterials;

        if (sourceMaterials == null || sourceMaterials.Length == 0)
            sourceMaterials = new Material[] { null };

        Material[] targetMaterials = new Material[sourceMaterials.Length];
        RendererBinding rendererBinding = new RendererBinding
        {
            Renderer = renderer,
            MaterialCount = targetMaterials.Length
        };

        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            Material sourceMaterial = sourceMaterials[i];
            Material targetMaterial = sourceMaterial != null &&
                                       sourceMaterial.HasProperty(BuildProgressId)
                ? sourceMaterial
                : runtimeRevealMaterial;

            targetMaterials[i] = targetMaterial;

            // SpriteRenderer supplies its sprite texture as a per-renderer
            // texture. For mesh renderers we preserve the old texture/color
            // through the same MaterialPropertyBlock.
            if (!(renderer is SpriteRenderer) && sourceMaterial != null)
            {
                if (sourceMaterial.HasProperty(MainTexId))
                {
                    Texture texture = sourceMaterial.GetTexture(MainTexId);

                    if (texture != null)
                        rendererBinding.PropertyBlock.SetTexture(MainTexId, texture);
                }

                if (sourceMaterial.HasProperty(ColorId))
                {
                    rendererBinding.PropertyBlock.SetColor(
                        ColorId,
                        sourceMaterial.GetColor(ColorId)
                    );
                }
            }
        }

        renderer.sharedMaterials = targetMaterials;
        return rendererBinding;
    }

    private GraphicBinding PrepareGraphic(Graphic graphic)
    {
        Material runtimeRevealMaterial = ResolveRevealMaterial();

        if (runtimeRevealMaterial == null)
            return null;

        Material sourceMaterial = graphic.material;
        Material graphicMaterial = sourceMaterial != null &&
                                    sourceMaterial.HasProperty(BuildProgressId)
            ? new Material(sourceMaterial)
            : new Material(runtimeRevealMaterial);

        graphicMaterial.name = $"BuildReveal_{graphic.name}";
        graphicMaterial.hideFlags = HideFlags.DontSave;
        graphic.material = graphicMaterial;

        return new GraphicBinding
        {
            Graphic = graphic,
            Material = graphicMaterial
        };
    }

    private Material ResolveRevealMaterial()
    {
        if (revealMaterial != null && revealMaterial.HasProperty(BuildProgressId))
            return revealMaterial;

        Shader shader = Shader.Find("Sprites/BuildReveal");

        if (shader == null)
            return null;

        if (revealMaterial == null || revealMaterial.shader != shader)
        {
            revealMaterial = new Material(shader)
            {
                name = "BuildReveal_RuntimeMaterial",
                hideFlags = HideFlags.DontSave
            };
        }

        return revealMaterial;
    }

    private static bool TryGetTargetBounds(
        BuildTarget target,
        out Bounds bounds
    )
    {
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < target.Renderers.Count; i++)
        {
            Renderer renderer = target.Renderers[i].Renderer;

            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        Vector3[] corners = new Vector3[4];

        for (int i = 0; i < target.Graphics.Count; i++)
        {
            Graphic graphic = target.Graphics[i].Graphic;

            if (graphic == null || graphic.rectTransform == null)
                continue;

            graphic.rectTransform.GetWorldCorners(corners);

            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            {
                if (!hasBounds)
                {
                    bounds = new Bounds(corners[cornerIndex], Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(corners[cornerIndex]);
                }
            }
        }

        if (!hasBounds)
            return false;

        if (bounds.size.y <= .0001f)
        {
            Vector3 center = bounds.center;
            bounds.Encapsulate(center + Vector3.up * .0001f);
            bounds.Encapsulate(center - Vector3.up * .0001f);
        }

        return true;
    }

    private void SetMaterialProperties(
        Material material,
        float progress,
        float topY,
        float bottomY,
        float softness,
        float glowWidth
    )
    {
        material.SetFloat(BuildProgressId, progress);
        material.SetFloat(RevealTopYId, topY);
        material.SetFloat(RevealBottomYId, bottomY);
        material.SetFloat(EdgeSoftnessId, softness);
        material.SetFloat(EdgeGlowWidthId, glowWidth);
        material.SetColor(EdgeGlowColorId, edgeGlowColor);
        material.SetFloat(EdgeGlowStrengthId, edgeGlowStrength);
    }

    private sealed class BuildTarget
    {
        public Transform Target;
        public Bounds Bounds;
        public Vector3 RestPosition;
        public Vector3 RestScale;
        public Vector3 LiftedPosition;
        public bool IsLifted;
        public readonly List<RendererBinding> Renderers =
            new List<RendererBinding>();
        public readonly List<GraphicBinding> Graphics =
            new List<GraphicBinding>();

        public bool HasVisuals => Renderers.Count > 0 || Graphics.Count > 0;
    }

    private sealed class RendererBinding
    {
        public Renderer Renderer;
        public int MaterialCount;
        public readonly MaterialPropertyBlock PropertyBlock =
            new MaterialPropertyBlock();
    }

    private sealed class GraphicBinding
    {
        public Graphic Graphic;
        public Material Material;
    }
}
