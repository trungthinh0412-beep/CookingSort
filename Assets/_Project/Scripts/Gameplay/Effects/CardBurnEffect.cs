using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class CardBurnEffect : MonoBehaviour
{
    private const string SettingsResourcePath = "CardBurn/CardBurnSettings";
    private const string BurnMaterialResourcePath = "CardBurn/CardBurnDissolve";
    private const string AdditiveGlowMaterialResourcePath =
        "CardBurn/CardBurnAdditiveGlow";
    private const string SparkMaterialResourcePath = "CardBurn/CardBurnSpark";

    private static readonly int DissolveAmountId =
        Shader.PropertyToID("_DissolveAmount");
    private static readonly int EdgeWidthId =
        Shader.PropertyToID("_EdgeWidth");
    private static readonly int EdgeColorId =
        Shader.PropertyToID("_EdgeColor");
    private static readonly int CoreColorId =
        Shader.PropertyToID("_CoreColor");
    private static readonly int EmissionStrengthId =
        Shader.PropertyToID("_EmissionStrength");
    private static readonly int GlowStrengthId =
        Shader.PropertyToID("_GlowStrength");
    private static readonly int NoiseScaleId =
        Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseSpeedId =
        Shader.PropertyToID("_NoiseSpeed");
    private static readonly int NoiseStrengthId =
        Shader.PropertyToID("_NoiseStrength");
    private static readonly int NoiseSeedId =
        Shader.PropertyToID("_NoiseSeed");
    private static readonly int FlashId = Shader.PropertyToID("_Flash");
    private static readonly int BurnModeId = Shader.PropertyToID("_BurnMode");
    private static readonly int BurnOriginId = Shader.PropertyToID("_BurnOrigin");
    private static readonly int SpriteBoundsId = Shader.PropertyToID("_SpriteBounds");
    private static readonly AnimationCurve EmberSizeOverLifetimeCurve =
        new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.35f, 0.82f),
            new Keyframe(0.72f, 0.42f),
            new Keyframe(1f, 0.05f)
        );

    [Header("Reusable Assets")]
    [SerializeField] private CardBurnSettings settings;
    [SerializeField] private Material burnMaterial;
    [SerializeField] private Material additiveGlowMaterial;
    [SerializeField] private Material sparkMaterial;

    [Header("Target")]
    [SerializeField] private SpriteRenderer[] targetRenderers;
    [SerializeField] private SpriteRenderer[] hideDuringBurn;

    private readonly List<RendererState> _rendererStates =
        new List<RendererState>(4);
    private readonly List<HiddenRendererState> _hiddenRendererStates =
        new List<HiddenRendererState>(4);
    private readonly List<GlowRendererState> _glowRendererStates =
        new List<GlowRendererState>(4);
    private Coroutine _burnRoutine;
    private ParticleSystem _sparks;
    private Action _onComplete;
    private bool _isResetting;

    public bool IsPlaying => _burnRoutine != null;

    private sealed class RendererState
    {
        public SpriteRenderer Renderer;
        public Material[] Materials;
        public MaterialPropertyBlock PropertyBlock;
        public MaterialPropertyBlock WorkBlock;
        public bool WasEnabled;
    }

    private sealed class HiddenRendererState
    {
        public SpriteRenderer Renderer;
        public bool WasEnabled;
    }

    private sealed class GlowRendererState
    {
        public SpriteRenderer Renderer;
        public MaterialPropertyBlock WorkBlock;
    }

    public void SetTargetRenderers(params SpriteRenderer[] renderers)
    {
        targetRenderers = renderers;
    }

    public void SetHiddenRenderers(params SpriteRenderer[] renderers)
    {
        hideDuringBurn = renderers;
    }

    public void PlayBurn(Action onComplete = null)
    {
        PlayBurn(-1f, -1, -1f, onComplete);
    }

    public void PlayBurn(
        float durationOverride,
        int sparkCountOverride,
        float sparkLifetimeOverride,
        Action onComplete = null)
    {
        ResetEffect(false);
        ResolveAssets();
        CacheRendererStates();

        if (settings == null || burnMaterial == null ||
            _rendererStates.Count == 0)
        {
            Debug.LogWarning(
                "Card Burn FX could not start because its settings, material, " +
                "or SpriteRenderer target is missing.",
                this
            );
            onComplete?.Invoke();
            return;
        }

        _onComplete = onComplete;
        ApplyBurnMaterial();
        HideAuxiliaryRenderers();
        PrepareAdditiveGlow();
        _burnRoutine = StartCoroutine(BurnRoutine(
            durationOverride,
            sparkCountOverride,
            sparkLifetimeOverride
        ));
    }

    public void ResetEffect()
    {
        ResetEffect(false);
    }

    [ContextMenu("Preview Burn")]
    private void PreviewBurn()
    {
        if (!Application.isPlaying)
        {
            Debug.Log(
                "Enter Play Mode, select this card, then use Preview Burn. " +
                "The Inspector also contains a preview button.",
                this
            );
            return;
        }

        PlayBurn();
    }

    [ContextMenu("Reset Burn")]
    private void PreviewReset()
    {
        ResetEffect();
    }

    private IEnumerator BurnRoutine(
        float durationOverride,
        int sparkCountOverride,
        float sparkLifetimeOverride)
    {
        float duration = durationOverride > 0f
            ? durationOverride
            : settings.duration;
        duration /= Mathf.Max(0.1f, settings.dissolveSpeed);
        float flashDuration = Mathf.Min(settings.flashDuration, duration * 0.3f);
        float burnDuration = Mathf.Max(0.05f, duration - flashDuration);
        int sparkCount = sparkCountOverride >= 0
            ? sparkCountOverride
            : settings.sparkCount;
        float sparkLifetime = sparkLifetimeOverride > 0f
            ? sparkLifetimeOverride
            : settings.sparkLifetime;
        float seed = Random.Range(0.05f, 100f);

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float progress = flashDuration > 0f
                ? Mathf.Clamp01(elapsed / flashDuration)
                : 1f;
            float flash = Mathf.Sin(progress * Mathf.PI);
            UpdateRenderers(0f, flash, seed);
            yield return null;
        }

        Bounds bounds = GetWorldBounds();
        PrepareSparks();
        int emittedSparks = 0;
        elapsed = 0f;

        while (elapsed < burnDuration)
        {
            elapsed += Time.deltaTime;
            float linearProgress = Mathf.Clamp01(elapsed / burnDuration);
            float dissolveProgress = linearProgress * linearProgress *
                                     (3f - 2f * linearProgress);
            UpdateRenderers(dissolveProgress, 0f, seed);

            float emberProgress = Mathf.InverseLerp(
                settings.sparkStartProgress,
                1f,
                linearProgress
            );
            int desiredSparkCount = Mathf.FloorToInt(
                sparkCount * Mathf.SmoothStep(0f, 1f, emberProgress)
            );
            while (emittedSparks < desiredSparkCount)
            {
                EmitSpark(bounds, dissolveProgress, sparkLifetime);
                emittedSparks++;
            }

            yield return null;
        }

        UpdateRenderers(1f, 0f, seed);
        HideBurnedSprites();
        while (emittedSparks < sparkCount)
        {
            EmitSpark(bounds, 1f, sparkLifetime);
            emittedSparks++;
        }

        float sparkTail = sparkCount > 0 ? sparkLifetime : 0f;
        if (sparkTail > 0f)
            yield return new WaitForSeconds(Mathf.Max(0.05f, sparkTail));

        _burnRoutine = null;
        Action callback = _onComplete;
        _onComplete = null;
        callback?.Invoke();
    }

    private void ResolveAssets()
    {
        if (settings == null)
            settings = Resources.Load<CardBurnSettings>(SettingsResourcePath);
        if (burnMaterial == null)
            burnMaterial = Resources.Load<Material>(BurnMaterialResourcePath);
        if (additiveGlowMaterial == null)
        {
            additiveGlowMaterial = Resources.Load<Material>(
                AdditiveGlowMaterialResourcePath
            );
        }
        if (sparkMaterial == null)
            sparkMaterial = Resources.Load<Material>(SparkMaterialResourcePath);

        if (burnMaterial != null &&
            (burnMaterial.shader == null || !burnMaterial.shader.isSupported))
        {
            Debug.LogError(
                "Card Burn dissolve shader is missing or unsupported. " +
                "The card will be removed without applying the pink error material.",
                this
            );
            burnMaterial = null;
        }
        if (sparkMaterial != null &&
            (sparkMaterial.shader == null || !sparkMaterial.shader.isSupported))
        {
            sparkMaterial = null;
        }
        if (additiveGlowMaterial != null &&
            (additiveGlowMaterial.shader == null ||
             !additiveGlowMaterial.shader.isSupported))
        {
            additiveGlowMaterial = null;
        }
    }

    private void CacheRendererStates()
    {
        _rendererStates.Clear();
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SpriteRenderer target = targetRenderers[i];
            if (target == null || target.sprite == null)
                continue;

            MaterialPropertyBlock originalBlock = new MaterialPropertyBlock();
            target.GetPropertyBlock(originalBlock);
            MaterialPropertyBlock workBlock = new MaterialPropertyBlock();
            target.GetPropertyBlock(workBlock);
            _rendererStates.Add(new RendererState
            {
                Renderer = target,
                Materials = target.sharedMaterials,
                PropertyBlock = originalBlock,
                WorkBlock = workBlock,
                WasEnabled = target.enabled
            });
        }
    }

    private void ApplyBurnMaterial()
    {
        for (int i = 0; i < _rendererStates.Count; i++)
        {
            RendererState state = _rendererStates[i];
            state.Renderer.sharedMaterial = burnMaterial;
            state.Renderer.enabled = state.WasEnabled;
        }
    }

    private void HideAuxiliaryRenderers()
    {
        _hiddenRendererStates.Clear();
        if (hideDuringBurn == null)
            return;

        for (int i = 0; i < hideDuringBurn.Length; i++)
        {
            SpriteRenderer renderer = hideDuringBurn[i];
            if (renderer == null || IsBurnTarget(renderer))
                continue;
            _hiddenRendererStates.Add(new HiddenRendererState
            {
                Renderer = renderer,
                WasEnabled = renderer.enabled
            });
            renderer.enabled = false;
        }
    }

    private void PrepareAdditiveGlow()
    {
        for (int i = 0; i < _glowRendererStates.Count; i++)
        {
            if (_glowRendererStates[i].Renderer != null)
                _glowRendererStates[i].Renderer.enabled = false;
        }

        if (additiveGlowMaterial == null)
            return;

        for (int i = 0; i < _rendererStates.Count; i++)
        {
            SpriteRenderer source = _rendererStates[i].Renderer;
            if (source == null || source.sprite == null)
                continue;

            GlowRendererState glowState;
            if (i < _glowRendererStates.Count &&
                _glowRendererStates[i].Renderer != null)
            {
                glowState = _glowRendererStates[i];
            }
            else
            {
                GameObject glowObject = new GameObject(
                    "CardBurnAdditiveGlow"
                );
                glowObject.layer = source.gameObject.layer;
                glowObject.transform.SetParent(source.transform, false);
                SpriteRenderer glowRenderer =
                    glowObject.AddComponent<SpriteRenderer>();
                glowState = new GlowRendererState
                {
                    Renderer = glowRenderer,
                    WorkBlock = new MaterialPropertyBlock()
                };

                if (i < _glowRendererStates.Count)
                    _glowRendererStates[i] = glowState;
                else
                    _glowRendererStates.Add(glowState);
            }

            SpriteRenderer glow = glowState.Renderer;
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localRotation = Quaternion.identity;
            glow.transform.localScale = Vector3.one *
                                        settings.additiveGlowScale;
            glow.sprite = source.sprite;
            glow.color = source.color;
            glow.flipX = source.flipX;
            glow.flipY = source.flipY;
            glow.drawMode = source.drawMode;
            glow.size = source.size;
            glow.maskInteraction = source.maskInteraction;
            glow.spriteSortPoint = source.spriteSortPoint;
            glow.sortingLayerID = source.sortingLayerID;
            glow.sortingOrder = source.sortingOrder + 1;
            glow.sharedMaterial = additiveGlowMaterial;
            glow.enabled = source.enabled;
        }
    }

    private bool IsBurnTarget(SpriteRenderer renderer)
    {
        for (int i = 0; i < _rendererStates.Count; i++)
        {
            if (_rendererStates[i].Renderer == renderer)
                return true;
        }
        return false;
    }

    private void UpdateRenderers(float dissolve, float flash, float seed)
    {
        for (int i = 0; i < _rendererStates.Count; i++)
        {
            RendererState state = _rendererStates[i];
            SpriteRenderer target = state.Renderer;
            if (target == null || target.sprite == null)
                continue;

            Bounds spriteBounds = target.sprite.bounds;
            MaterialPropertyBlock block = state.WorkBlock;
            block.SetFloat(DissolveAmountId, dissolve);
            block.SetFloat(FlashId, flash);
            block.SetFloat(EdgeWidthId, settings.burnEdgeWidth);
            block.SetColor(EdgeColorId, settings.burnEdgeColor);
            block.SetColor(CoreColorId, settings.burnCoreColor);
            block.SetFloat(EmissionStrengthId, settings.emissionStrength);
            block.SetFloat(NoiseScaleId, settings.noiseScale);
            block.SetFloat(NoiseSpeedId, settings.noiseSpeed);
            block.SetFloat(NoiseStrengthId, settings.noiseStrength);
            block.SetFloat(NoiseSeedId, seed + i * 3.17f);
            block.SetFloat(
                BurnModeId,
                settings.direction == CardBurnSettings.BurnDirection.BottomUp
                    ? 0f
                    : 1f
            );
            block.SetVector(BurnOriginId, new Vector4(
                settings.burnOrigin.x,
                settings.burnOrigin.y,
                0f,
                0f
            ));
            block.SetVector(SpriteBoundsId, new Vector4(
                spriteBounds.min.x,
                spriteBounds.min.y,
                spriteBounds.size.x,
                spriteBounds.size.y
            ));
            target.SetPropertyBlock(block);

            if (i >= _glowRendererStates.Count)
                continue;

            GlowRendererState glowState = _glowRendererStates[i];
            SpriteRenderer glow = glowState.Renderer;
            if (glow == null || !glow.enabled)
                continue;

            MaterialPropertyBlock glowBlock = glowState.WorkBlock;
            glowBlock.SetFloat(DissolveAmountId, dissolve);
            glowBlock.SetFloat(FlashId, flash);
            glowBlock.SetFloat(EdgeWidthId, settings.burnEdgeWidth);
            glowBlock.SetColor(EdgeColorId, settings.burnEdgeColor);
            glowBlock.SetColor(CoreColorId, settings.burnCoreColor);
            glowBlock.SetFloat(
                GlowStrengthId,
                settings.additiveGlowStrength
            );
            glowBlock.SetFloat(NoiseScaleId, settings.noiseScale);
            glowBlock.SetFloat(NoiseSpeedId, settings.noiseSpeed);
            glowBlock.SetFloat(NoiseStrengthId, settings.noiseStrength);
            glowBlock.SetFloat(NoiseSeedId, seed + i * 3.17f);
            glowBlock.SetFloat(
                BurnModeId,
                settings.direction == CardBurnSettings.BurnDirection.BottomUp
                    ? 0f
                    : 1f
            );
            glowBlock.SetVector(BurnOriginId, new Vector4(
                settings.burnOrigin.x,
                settings.burnOrigin.y,
                0f,
                0f
            ));
            glowBlock.SetVector(SpriteBoundsId, new Vector4(
                spriteBounds.min.x,
                spriteBounds.min.y,
                spriteBounds.size.x,
                spriteBounds.size.y
            ));
            glow.SetPropertyBlock(glowBlock);
        }
    }

    private void HideBurnedSprites()
    {
        for (int i = 0; i < _rendererStates.Count; i++)
        {
            if (_rendererStates[i].Renderer != null)
                _rendererStates[i].Renderer.enabled = false;
        }

        for (int i = 0; i < _glowRendererStates.Count; i++)
        {
            if (_glowRendererStates[i].Renderer != null)
                _glowRendererStates[i].Renderer.enabled = false;
        }
    }

    private Bounds GetWorldBounds()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < _rendererStates.Count; i++)
        {
            SpriteRenderer renderer = _rendererStates[i].Renderer;
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
        return bounds;
    }

    private void PrepareSparks()
    {
        if (_sparks == null)
        {
            GameObject sparkObject = new GameObject("CardBurnSparks");
            sparkObject.transform.SetParent(transform, false);
            _sparks = sparkObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = _sparks.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // Gas Lighter can burn a complete matching stack at once. Keep a
            // sufficiently large reusable pool so the gold ember burst is
            // never clipped when several cards burn together.
            main.maxParticles = 1800;
            main.startSpeed = 0f;
            main.startSize = 0.05f;
            main.startLifetime = 0.4f;

            ParticleSystem.EmissionModule emission = _sparks.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _sparks.shape;
            shape.enabled = false;
            // Embers cool and lose volume as they rise. This curve preserves
            // their initial yellow flash, then shrinks them naturally before
            // their lifetime ends.
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                _sparks.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                EmberSizeOverLifetimeCurve
            );
            // A gentle lateral noise makes the few embers curl around one
            // another while their base velocity still carries them upward.
            ParticleSystem.NoiseModule noise = _sparks.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = .1f;
            noise.strengthY = .025f;
            noise.strengthZ = 0f;
            noise.frequency = .55f;
            noise.scrollSpeed = .25f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.High;
            ParticleSystemRenderer particleRenderer =
                sparkObject.GetComponent<ParticleSystemRenderer>();
            // Reuse the gold dust sparkle texture; billboard particles stay
            // parallel to the XY card face and scatter across that plane.
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        ParticleSystemRenderer renderer =
            _sparks.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sparkMaterial;
        if (_rendererStates.Count > 0 && _rendererStates[0].Renderer != null)
        {
            SpriteRenderer cardRenderer = _rendererStates[0].Renderer;
            renderer.sortingLayerID = cardRenderer.sortingLayerID;
            renderer.sortingOrder = cardRenderer.sortingOrder + 2;
        }
        _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _sparks.Play();
    }

    private void EmitSpark(Bounds bounds, float progress, float lifetime)
    {
        if (_sparks == null || sparkMaterial == null)
            return;

        float frontY = Mathf.Lerp(bounds.min.y, bounds.max.y, progress);
        Vector3 position = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            frontY + Random.Range(-bounds.size.y * 0.06f, bounds.size.y * 0.07f),
            bounds.center.z
        );
        float riseSpeed = Random.Range(0.85f, 1.15f) *
                          settings.sparkRiseSpeed;
        Vector3 velocity = new Vector3(
            Random.Range(-0.08f, 0.08f) * riseSpeed,
            riseSpeed,
            0f
        );
        float size = settings.sparkSize * Random.Range(0.65f, 1.3f);
        Color color = Color.Lerp(
            settings.sparkColor,
            settings.burnCoreColor,
            Random.Range(0f, 0.45f)
        );

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startLifetime = lifetime * Random.Range(0.72f, 1.15f),
            startSize = size,
            startColor = color,
            rotation = Random.Range(-25f, 25f)
        };
        _sparks.Emit(emit, 1);
    }

    private void ResetEffect(bool invokeCallback)
    {
        if (_isResetting)
            return;
        _isResetting = true;

        if (_burnRoutine != null)
        {
            StopCoroutine(_burnRoutine);
            _burnRoutine = null;
        }
        if (_sparks != null)
            _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        for (int i = 0; i < _rendererStates.Count; i++)
        {
            RendererState state = _rendererStates[i];
            if (state.Renderer == null)
                continue;
            state.Renderer.sharedMaterials = state.Materials;
            state.Renderer.SetPropertyBlock(state.PropertyBlock);
            state.Renderer.enabled = state.WasEnabled;
        }
        _rendererStates.Clear();
        for (int i = 0; i < _hiddenRendererStates.Count; i++)
        {
            HiddenRendererState state = _hiddenRendererStates[i];
            if (state.Renderer != null)
                state.Renderer.enabled = state.WasEnabled;
        }
        _hiddenRendererStates.Clear();
        for (int i = 0; i < _glowRendererStates.Count; i++)
        {
            GlowRendererState glowState = _glowRendererStates[i];
            if (glowState.Renderer != null)
            {
                glowState.Renderer.enabled = false;
                glowState.Renderer.SetPropertyBlock(null);
            }
        }

        Action callback = _onComplete;
        _onComplete = null;
        _isResetting = false;
        if (invokeCallback)
            callback?.Invoke();
    }

    private void OnDisable()
    {
        ResetEffect(false);
    }
}
