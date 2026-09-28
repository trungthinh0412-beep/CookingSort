using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BuildButtonAvailabilityVFX : MonoBehaviour
{
    [Header("Particle systems")]
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private ParticleSystem sparkle;

    [Header("Emission")]
    [SerializeField] [Min(0f)] private float dustEmissionRate = 14f;
    [SerializeField] [Min(0f)] private float sparkleEmissionRate = 5f;
    [SerializeField] [Min(0f)] private float borderPadding = 10f;

    [Header("Style")]
    [SerializeField] private Color dustColor = new Color(1f, .72f, .2f, .88f);
    [SerializeField] private Color sparkleColor = new Color(1f, .96f, .55f, 1f);
    [SerializeField] [Min(0f)] private float dustSizeRatio = .045f;
    [SerializeField] [Min(0f)] private float sparkleSizeRatio = .065f;

    [Header("Renderer")]
    [SerializeField] private Material dustMaterial;
    [SerializeField] private Material sparkleMaterial;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int dustSortingOrder = 1206;
    [SerializeField] private int sparkleSortingOrder = 1207;

    [Header("Behaviour")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Screen-space UI fallback")]
    [Tooltip("Screen-space overlay canvases cannot draw a ParticleSystemRenderer above UI reliably. These pooled UI sprites provide the same dust/sparkle effect in that case.")]
    [SerializeField] [Min(0)] private int uiDustCount = 34;
    [SerializeField] [Min(0)] private int uiSparkleCount = 10;
    [SerializeField] [Min(0f)] private float uiParticlePadding = 16f;

    private bool isAvailable;
    private bool isConfigured;
    private Vector2 lastRectSize;
    private RectTransform uiParticleRoot;
    private float uiAnimationTime;
    private readonly List<UiParticle> uiParticles = new List<UiParticle>();
    private Material fallbackDustMaterial;
    private Material fallbackSparkleMaterial;
    private Texture2D fallbackDustTexture;
    private Texture2D fallbackSparkleTexture;
    private Sprite fallbackDustSprite;
    private Sprite fallbackSparkleSprite;

    private sealed class UiParticle
    {
        public RectTransform RectTransform;
        public Image Image;
        public bool IsSparkle;
        public float Perimeter;
        public float Phase;
        public float Duration;
        public float SizeMultiplier = 1f;
        public float Rotation;
        public float RotationSpeed;
        public float Drift;
    }

    public bool IsAvailable => isAvailable;

    private void Awake()
    {
        EnsureParticleSystems();
        ConfigureParticleSystems();
        EnsureUiParticleVisuals();
        RefreshParticleShapeIfNeeded(force: true);
        StopParticles();
    }

    private void OnEnable()
    {
        if (!isAvailable)
            StopParticles();
    }

    private void LateUpdate()
    {
        if (!isAvailable)
            return;

        RefreshParticleShapeIfNeeded();
        UpdateUiParticles(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
    }

    private void OnDisable()
    {
        isAvailable = false;
        StopParticles();
    }

    private void OnDestroy()
    {
        DestroyRuntimeObject(fallbackDustSprite);
        DestroyRuntimeObject(fallbackSparkleSprite);
        DestroyRuntimeObject(fallbackDustMaterial);
        DestroyRuntimeObject(fallbackSparkleMaterial);
        DestroyRuntimeObject(fallbackDustTexture);
        DestroyRuntimeObject(fallbackSparkleTexture);
    }

    public void SetAvailable(bool available)
    {
        if (available == isAvailable)
        {
            if (available)
                RefreshParticleShapeIfNeeded();

            return;
        }

        isAvailable = available;

        if (!available)
        {
            StopParticles();
            return;
        }

        EnsureParticleSystems();
        ConfigureParticleSystems();
        EnsureUiParticleVisuals();
        ResetUiParticles();
        RefreshParticleShapeIfNeeded(force: true);
        SetUiParticlesVisible(true);
        UpdateUiParticles(0f);

        dust?.Clear(true);
        sparkle?.Clear(true);

        dust?.Play(true);
        sparkle?.Play(true);

        // Give the effect a visible first beat instead of waiting for the
        // first rate-over-time tick after the marker becomes available.
        if (dust != null)
            dust.Emit(Mathf.Clamp(Mathf.RoundToInt(dustEmissionRate * .18f), 2, 5));

        if (sparkle != null)
            sparkle.Emit(Mathf.Clamp(Mathf.RoundToInt(sparkleEmissionRate * .2f), 1, 2));
    }

    public void StopEffect()
    {
        isAvailable = false;
        StopParticles();
    }

    private void EnsureParticleSystems()
    {
        dust = FindOrCreateParticleSystem(dust, "BuildButtonDust");
        sparkle = FindOrCreateParticleSystem(sparkle, "BuildButtonSparkle");
    }

    private void EnsureUiParticleVisuals()
    {
        if (uiParticleRoot == null)
        {
            GameObject rootObject = new GameObject("BuildButtonAvailabilityUI");
            rootObject.layer = gameObject.layer;
            uiParticleRoot = rootObject.AddComponent<RectTransform>();
            uiParticleRoot.SetParent(transform, false);
            uiParticleRoot.anchorMin = Vector2.zero;
            uiParticleRoot.anchorMax = Vector2.one;
            uiParticleRoot.anchoredPosition = Vector2.zero;
            uiParticleRoot.sizeDelta = Vector2.zero;
            uiParticleRoot.pivot = new Vector2(.5f, .5f);
            uiParticleRoot.SetAsLastSibling();
        }

        int requiredCount = GetConfiguredUiParticleCount();

        while (uiParticles.Count < requiredCount)
        {
            int particleIndex = uiParticles.Count;
            bool isSparkle = particleIndex >= Mathf.Max(0, uiDustCount);

            GameObject particleObject = new GameObject(
                isSparkle ? "BuildButtonUiSparkle" : "BuildButtonUiDust"
            );
            particleObject.layer = gameObject.layer;

            RectTransform particleRect = particleObject.AddComponent<RectTransform>();
            particleRect.SetParent(uiParticleRoot, false);
            particleRect.anchorMin = new Vector2(.5f, .5f);
            particleRect.anchorMax = new Vector2(.5f, .5f);
            particleRect.pivot = new Vector2(.5f, .5f);

            Image image = particleObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.maskable = false;
            image.sprite = GetFallbackParticleSprite(isSparkle);
            image.color = Color.clear;
            image.enabled = false;

            uiParticles.Add(new UiParticle
            {
                RectTransform = particleRect,
                Image = image,
                IsSparkle = isSparkle
            });
        }

        uiParticleRoot.SetAsLastSibling();
    }

    private int GetConfiguredUiParticleCount()
    {
        return Mathf.Max(0, uiDustCount) + Mathf.Max(0, uiSparkleCount);
    }

    private void ResetUiParticles()
    {
        uiAnimationTime = 0f;

        int dustCount = Mathf.Max(0, uiDustCount);
        int activeCount = Mathf.Min(uiParticles.Count, GetConfiguredUiParticleCount());

        for (int i = 0; i < uiParticles.Count; i++)
        {
            UiParticle particle = uiParticles[i];
            bool active = i < activeCount;

            particle.IsSparkle = i >= dustCount;
            particle.Image.sprite = GetFallbackParticleSprite(particle.IsSparkle);
            particle.Image.enabled = false;
            particle.Image.color = Color.clear;

            if (!active)
                continue;

            particle.Perimeter = Random.value;
            particle.Duration = particle.IsSparkle
                ? Random.Range(1.15f, 1.7f)
                : Random.Range(.9f, 1.45f);
            particle.Phase = Random.Range(0f, particle.Duration);
            particle.SizeMultiplier = Random.Range(.78f, 1.22f);
            particle.Rotation = Random.Range(-180f, 180f);
            particle.RotationSpeed = Random.Range(-42f, 42f);
            particle.Drift = Random.Range(-4f, 4f);
        }
    }

    private void SetUiParticlesVisible(bool visible)
    {
        int activeCount = Mathf.Min(uiParticles.Count, GetConfiguredUiParticleCount());

        for (int i = 0; i < uiParticles.Count; i++)
        {
            UiParticle particle = uiParticles[i];
            bool shouldBeVisible = visible && i < activeCount;

            particle.Image.enabled = shouldBeVisible;

            if (!shouldBeVisible)
            {
                Color color = particle.Image.color;
                color.a = 0f;
                particle.Image.color = color;
            }
        }
    }

    private void UpdateUiParticles(float deltaTime)
    {
        if (!isAvailable || uiParticleRoot == null || uiParticles.Count == 0)
            return;

        uiAnimationTime += Mathf.Max(0f, deltaTime);

        int activeCount = Mathf.Min(uiParticles.Count, GetConfiguredUiParticleCount());
        float width = Mathf.Max(1f, lastRectSize.x + uiParticlePadding * 2f);
        float height = Mathf.Max(1f, lastRectSize.y + uiParticlePadding * 2f);

        for (int i = 0; i < uiParticles.Count; i++)
        {
            UiParticle particle = uiParticles[i];

            if (i >= activeCount || particle.Image == null || particle.RectTransform == null)
                continue;

            float progress = Mathf.Repeat(
                (uiAnimationTime + particle.Phase) / Mathf.Max(.01f, particle.Duration),
                1f
            );
            float lifeFade = Mathf.Sin(progress * Mathf.PI);
            float pulse = particle.IsSparkle
                ? Mathf.Lerp(.72f, 1.16f, Mathf.Sin(progress * Mathf.PI))
                : Mathf.Lerp(.72f, 1f, lifeFade);

            Vector2 point = GetPerimeterPoint(
                particle.Perimeter + progress * (particle.IsSparkle ? .035f : .075f),
                width,
                height,
                out Vector2 normal
            );

            float outwardMotion = particle.IsSparkle
                ? Mathf.Sin(progress * Mathf.PI) * 2.5f
                : progress * 3.5f;
            point += normal * outwardMotion;
            point += new Vector2(
                Mathf.Sin((uiAnimationTime + particle.Phase) * 2.4f) * particle.Drift,
                Mathf.Cos((uiAnimationTime + particle.Phase) * 2.1f) * 2.5f
            );

            particle.RectTransform.anchoredPosition = point;
            particle.RectTransform.localRotation = Quaternion.Euler(
                0f,
                0f,
                particle.Rotation + particle.RotationSpeed * uiAnimationTime
            );

            float alpha = lifeFade * (particle.IsSparkle ? 1f : .92f);
            Color color = particle.IsSparkle ? sparkleColor : dustColor;
            color.a *= alpha;
            particle.Image.color = color;
            particle.Image.enabled = true;

            float scale = pulse * particle.SizeMultiplier;
            particle.RectTransform.localScale = Vector3.one * scale;
        }
    }

    private static Vector2 GetPerimeterPoint(
        float normalizedPosition,
        float width,
        float height,
        out Vector2 normal
    )
    {
        float halfWidth = width * .5f;
        float halfHeight = height * .5f;
        float perimeter = Mathf.Max(.01f, (width + height) * 2f);
        float distance = Mathf.Repeat(normalizedPosition, 1f) * perimeter;

        if (distance <= width)
        {
            normal = Vector2.up;
            return new Vector2(-halfWidth + distance, halfHeight);
        }

        distance -= width;

        if (distance <= height)
        {
            normal = Vector2.right;
            return new Vector2(halfWidth, halfHeight - distance);
        }

        distance -= height;

        if (distance <= width)
        {
            normal = Vector2.down;
            return new Vector2(halfWidth - distance, -halfHeight);
        }

        distance -= width;
        normal = Vector2.left;
        return new Vector2(-halfWidth, -halfHeight + distance);
    }

    private ParticleSystem FindOrCreateParticleSystem(
        ParticleSystem current,
        string childName
    )
    {
        if (current != null)
            return current;

        Transform child = transform.Find(childName);

        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(transform, false);
        }

        ParticleSystem particleSystem = child.GetComponent<ParticleSystem>();

        if (particleSystem == null)
            particleSystem = child.gameObject.AddComponent<ParticleSystem>();

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return particleSystem;
    }

    private void ConfigureParticleSystems()
    {
        if (isConfigured)
            return;

        EnsureParticleSystems();

        ConfigureParticleSystem(
            dust,
            dustMaterial,
            .52f,
            .9f,
            2f,
            8f,
            dustColor,
            dustSortingOrder,
            7401,
            9f,
            14f
        );

        ConfigureParticleSystem(
            sparkle,
            sparkleMaterial,
            .34f,
            .7f,
            .5f,
            4f,
            sparkleColor,
            sparkleSortingOrder,
            7402,
            12f,
            20f
        );

        isConfigured = true;
    }

    private void ConfigureParticleSystem(
        ParticleSystem particleSystem,
        Material material,
        float lifetimeMin,
        float lifetimeMax,
        float speedMin,
        float speedMax,
        Color color,
        int sortingOrder,
        uint randomSeed,
        float horizontalDrift,
        float upwardVelocity
    )
    {
        if (particleSystem == null)
            return;

        particleSystem.randomSeed = randomSeed;
        particleSystem.useAutoRandomSeed = false;

        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.useUnscaledTime = useUnscaledTime;
        main.maxParticles = 64;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            lifetimeMin,
            lifetimeMax
        );
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startColor = new ParticleSystem.MinMaxGradient(color);
        main.startRotation = new ParticleSystem.MinMaxCurve(
            -Mathf.PI,
            Mathf.PI
        );

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(
            particleSystem == dust ? dustEmissionRate : sparkleEmissionRate
        );

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        // Emit on the outside edge so the effect frames the marker without
        // covering its text or Gem icon.
        shape.shapeType = ParticleSystemShapeType.BoxEdge;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = new Vector3(300f, 100f, .01f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            CreateLifetimeGradient(color)
        );

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
            particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            CreateSizeCurve()
        );

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime =
            particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(
            -horizontalDrift,
            horizontalDrift
        );
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(
            upwardVelocity * .45f,
            upwardVelocity
        );
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-.5f, .5f);

        ParticleSystemRenderer renderer =
            particleSystem.GetComponent<ParticleSystemRenderer>();

        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.allowRoll = true;
            renderer.sharedMaterial = material != null
                ? material
                : GetFallbackParticleMaterial(particleSystem == sparkle);
        }

        particleSystem.Clear(true);
    }

    private void RefreshParticleShapeIfNeeded(bool force = false)
    {
        RectTransform rectTransform = transform as RectTransform;
        Vector2 rectSize = rectTransform != null
            ? rectTransform.rect.size
            : new Vector2(300f, 100f);

        if (rectSize.x <= .01f || rectSize.y <= .01f)
            rectSize = new Vector2(300f, 100f);

        if (!force && (rectSize - lastRectSize).sqrMagnitude < .01f)
            return;

        lastRectSize = rectSize;
        float smallestSide = Mathf.Max(1f, Mathf.Min(rectSize.x, rectSize.y));
        float padding = Mathf.Max(2f, borderPadding);

        SetParticleShape(
            dust,
            rectSize.x + padding * 2f,
            rectSize.y + padding * 2f
        );
        SetParticleShape(
            sparkle,
            rectSize.x + padding * 2.4f,
            rectSize.y + padding * 2.4f
        );

        SetParticleSize(
            dust,
            Mathf.Max(2f, smallestSide * dustSizeRatio),
            Mathf.Max(4f, smallestSide * dustSizeRatio * 2.1f)
        );
        SetParticleSize(
            sparkle,
            Mathf.Max(2f, smallestSide * sparkleSizeRatio),
            Mathf.Max(5f, smallestSide * sparkleSizeRatio * 1.6f)
        );

        float perimeterRatio = Mathf.Clamp(
            (rectSize.x + rectSize.y) / 548f,
            .55f,
            2.2f
        );
        SetEmissionRate(dust, dustEmissionRate * perimeterRatio);
        SetEmissionRate(sparkle, sparkleEmissionRate * perimeterRatio);

        RefreshUiParticleLayout(rectSize, smallestSide);
    }

    private void RefreshUiParticleLayout(Vector2 rectSize, float smallestSide)
    {
        int dustCount = Mathf.Max(0, uiDustCount);

        for (int i = 0; i < uiParticles.Count; i++)
        {
            UiParticle particle = uiParticles[i];

            if (particle.RectTransform == null)
                continue;

            particle.IsSparkle = i >= dustCount;
            float baseSize = particle.IsSparkle
                ? Mathf.Max(7f, smallestSide * sparkleSizeRatio)
                : Mathf.Max(5f, smallestSide * dustSizeRatio);

            particle.RectTransform.sizeDelta = Vector2.one * (
                baseSize * Mathf.Clamp(particle.SizeMultiplier, .7f, 1.3f)
            );
        }
    }

    private static void SetParticleShape(
        ParticleSystem particleSystem,
        float width,
        float height
    )
    {
        if (particleSystem == null)
            return;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.scale = new Vector3(
            Mathf.Max(1f, width),
            Mathf.Max(1f, height),
            .01f
        );
    }

    private static void SetParticleSize(
        ParticleSystem particleSystem,
        float min,
        float max
    )
    {
        if (particleSystem == null)
            return;

        ParticleSystem.MainModule main = particleSystem.main;
        main.startSize = new ParticleSystem.MinMaxCurve(min, max);
    }

    private static void SetEmissionRate(ParticleSystem particleSystem, float rate)
    {
        if (particleSystem == null)
            return;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.Max(0f, rate));
    }

    private void StopParticles()
    {
        StopAndClear(dust);
        StopAndClear(sparkle);
        SetUiParticlesVisible(false);
    }

    private static void StopAndClear(ParticleSystem particleSystem)
    {
        if (particleSystem != null)
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private Material GetFallbackParticleMaterial(bool sparkleVisual)
    {
        Material fallbackMaterial = sparkleVisual
            ? fallbackSparkleMaterial
            : fallbackDustMaterial;

        if (fallbackMaterial != null)
            return fallbackMaterial;

        Shader shader = Shader.Find("Particles/Standard Unlit");

        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        fallbackMaterial = new Material(shader)
        {
            name = sparkleVisual
                ? "BuildButtonSparkle_FallbackMaterial"
                : "BuildButtonDust_FallbackMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };

        Texture2D texture = GetFallbackParticleTexture(sparkleVisual);

        if (fallbackMaterial.HasProperty("_MainTex"))
            fallbackMaterial.SetTexture("_MainTex", texture);

        if (fallbackMaterial.HasProperty("_TintColor"))
            fallbackMaterial.SetColor("_TintColor", Color.white);

        if (fallbackMaterial.HasProperty("_Color"))
            fallbackMaterial.SetColor("_Color", Color.white);

        if (sparkleVisual)
            fallbackSparkleMaterial = fallbackMaterial;
        else
            fallbackDustMaterial = fallbackMaterial;

        return fallbackMaterial;
    }

    private Sprite GetFallbackParticleSprite(bool sparkleVisual)
    {
        Sprite sprite = sparkleVisual
            ? fallbackSparkleSprite
            : fallbackDustSprite;

        if (sprite != null)
            return sprite;

        sprite = Sprite.Create(
            GetFallbackParticleTexture(sparkleVisual),
            new Rect(0f, 0f, 32f, 32f),
            new Vector2(.5f, .5f),
            32f
        );
        sprite.name = sparkleVisual
            ? "BuildButtonSparkle_FallbackSprite"
            : "BuildButtonDust_FallbackSprite";
        sprite.hideFlags = HideFlags.HideAndDontSave;

        if (sparkleVisual)
            fallbackSparkleSprite = sprite;
        else
            fallbackDustSprite = sprite;

        return sprite;
    }

    private Texture2D GetFallbackParticleTexture(bool sparkleVisual)
    {
        Texture2D texture = sparkleVisual
            ? fallbackSparkleTexture
            : fallbackDustTexture;

        if (texture != null)
            return texture;

        const int textureSize = 32;
        texture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false,
            true
        )
        {
            name = sparkleVisual
                ? "BuildButtonSparkle_FallbackTexture"
                : "BuildButtonDust_FallbackTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[textureSize * textureSize];
        Vector2 center = Vector2.one * ((textureSize - 1) * .5f);
        float halfSize = textureSize * .5f;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float distance = delta.magnitude / halfSize;
                float alpha;

                if (!sparkleVisual)
                {
                    alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                }
                else
                {
                    float horizontal = Mathf.Clamp01(1f - Mathf.Abs(delta.x) / halfSize);
                    float vertical = Mathf.Clamp01(1f - Mathf.Abs(delta.y) / halfSize);
                    float diagonal = Mathf.Clamp01(
                        1f - Mathf.Abs(delta.x - delta.y) / (halfSize * 1.2f)
                    );
                    float otherDiagonal = Mathf.Clamp01(
                        1f - Mathf.Abs(delta.x + delta.y) / (halfSize * 1.2f)
                    );
                    float radial = Mathf.Clamp01(1f - distance * 1.35f);
                    alpha = Mathf.Max(
                        radial * .9f,
                        horizontal * vertical * .7f,
                        diagonal * .48f,
                        otherDiagonal * .48f
                    );
                    alpha *= alpha;
                }

                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        if (sparkleVisual)
            fallbackSparkleTexture = texture;
        else
            fallbackDustTexture = texture;

        return texture;
    }

    private static Gradient CreateLifetimeGradient(Color color)
    {
        Color transparent = new Color(color.r, color.g, color.b, 0f);
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, .45f),
                new GradientColorKey(transparent, 1f)
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(color.a * .65f, .5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return gradient;
    }

    private static AnimationCurve CreateSizeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, .35f),
            new Keyframe(.16f, 1f),
            new Keyframe(.7f, .75f),
            new Keyframe(1f, 0f)
        );
    }

    private static void DestroyRuntimeObject(Object runtimeObject)
    {
        if (runtimeObject == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeObject);
        else
            DestroyImmediate(runtimeObject);
    }
}
