using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BuildRevealVFX : MonoBehaviour
{
    [Header("Particle systems")]
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private ParticleSystem sparkle;
    [SerializeField] private ParticleSystem edgeDust;
    [SerializeField] private ParticleSystem finishBurst;

    [Header("Base emission")]
    [SerializeField] [Min(0f)] private float dustEmissionRate = 40f;
    [SerializeField] [Min(0f)] private float sparkleEmissionRate = 6f;
    [SerializeField] [Min(0f)] private float edgeDustEmissionRate = 48f;
    [SerializeField] [Min(0f)] private float finishBurstAmount = 10f;

    [Header("Finish shine")]
    [SerializeField] private Material shineMaterial;
    [SerializeField] private Color shineColor = new Color(1f, .78f, .2f, .82f);
    [SerializeField] [Min(.05f)] private float shineDuration = .52f;
    [SerializeField] [Range(.02f, .2f)] private float shineWidth = .075f;
    [SerializeField] [Range(-80f, 80f)] private float shineAngle = 28f;

    [Header("Renderer")]
    [SerializeField] private Material dustMaterial;
    [SerializeField] private Material sparkleMaterial;
    [SerializeField] private Material edgeDustMaterial;
    [SerializeField] private Material finishBurstMaterial;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 1200;

    [Header("Lifetime")]
    [SerializeField] [Min(0f)] private float cleanupDelay = .85f;
    [SerializeField] private bool destroyAfterFinish = true;
    [SerializeField] private bool useUnscaledTime = true;

    private Coroutine cleanupRoutine;
    private float activeDustAmount = 1f;
    private float activeSparkleAmount = 1f;
    private float activeVerticalOffset;
    private float activeWidthMultiplier = 1f;
    private float activeEmissionScale = 1f;
    private bool isPlaying;
    private Coroutine shineRoutine;
    private Image shineImage;
    private SpriteRenderer shineRenderer;
    private Sprite shineSprite;
    private Sprite targetMaskSprite;
    private Texture2D shineTexture;
    private Material shineUiMaterial;

    public ParticleSystem Dust => dust;
    public ParticleSystem Sparkle => sparkle;
    public ParticleSystem EdgeDust => edgeDust;
    public ParticleSystem FinishBurst => finishBurst;

    private void Awake()
    {
        EnsureParticleSystems();
        ConfigureParticleSystems();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying &&
            dust != null && sparkle != null && edgeDust != null && finishBurst != null)
        {
            ConfigureParticleSystems();
        }
    }
#endif

    public void SetParticleSystems(
        ParticleSystem dustSystem,
        ParticleSystem sparkleSystem,
        ParticleSystem edgeDustSystem,
        ParticleSystem finishBurstSystem
    )
    {
        dust = dustSystem;
        sparkle = sparkleSystem;
        edgeDust = edgeDustSystem;
        finishBurst = finishBurstSystem;
        ConfigureParticleSystems();
    }

    public void SetParticleMaterials(
        Material dustMaterialReference,
        Material sparkleMaterialReference,
        Material edgeDustMaterialReference,
        Material finishBurstMaterialReference
    )
    {
        dustMaterial = dustMaterialReference;
        sparkleMaterial = sparkleMaterialReference;
        edgeDustMaterial = edgeDustMaterialReference;
        finishBurstMaterial = finishBurstMaterialReference;

        if (shineMaterial == null)
            shineMaterial = sparkleMaterialReference;

        ConfigureParticleSystems();
    }

    public void Play(
        Bounds targetBounds,
        float dustAmount,
        float sparkleAmount,
        float verticalOffset,
        float widthMultiplier
    )
    {
        Play(
            null,
            targetBounds,
            dustAmount,
            sparkleAmount,
            verticalOffset,
            widthMultiplier
        );
    }

    public void Play(
        Transform target,
        Bounds targetBounds,
        float dustAmount,
        float sparkleAmount,
        float verticalOffset,
        float widthMultiplier
    )
    {
        EnsureParticleSystems();
        ConfigureParticleSystems();
        targetMaskSprite = FindTargetSprite(target);

        if (cleanupRoutine != null)
        {
            StopCoroutine(cleanupRoutine);
            cleanupRoutine = null;
        }

        gameObject.SetActive(true);
        activeDustAmount = Mathf.Max(0f, dustAmount);
        activeSparkleAmount = Mathf.Max(0f, sparkleAmount);
        activeVerticalOffset = verticalOffset;
        activeWidthMultiplier = Mathf.Max(.01f, widthMultiplier);
        activeEmissionScale = Mathf.Clamp(
            Mathf.Sqrt(
                targetBounds.size.x /
                Mathf.Max(.01f, targetBounds.size.y)
            ),
            .8f,
            1.8f
        );
        isPlaying = true;

        StopAndClear(finishBurst);
        ApplyEmissionRates();
        SetRevealEdge(targetBounds, 0f);

        dust?.Play(true);
        sparkle?.Play(true);
        edgeDust?.Play(true);
    }

    public void SetRevealEdge(Bounds targetBounds, float buildProgress)
    {
        if (!isPlaying && finishBurst == null)
            return;

        float progress = Mathf.Clamp01(buildProgress);
        float currentY = Mathf.Lerp(
            targetBounds.max.y,
            targetBounds.min.y,
            progress
        ) + activeVerticalOffset;

        float width = Mathf.Max(
            .01f,
            targetBounds.size.x * activeWidthMultiplier
        );
        float lineHeight = Mathf.Max(
            .01f,
            targetBounds.size.y * .035f
        );
        float depth = Mathf.Max(.02f, targetBounds.size.z * .5f);

        transform.position = new Vector3(
            targetBounds.center.x,
            currentY,
            targetBounds.center.z
        );

        SetShapeScale(dust, width, lineHeight, depth);
        SetShapeScale(sparkle, width, lineHeight * 1.2f, depth);
        SetShapeScale(edgeDust, width, lineHeight * 1.5f, depth);
        SetShapeScale(finishBurst, width * .65f, lineHeight * 2f, depth);
    }

    public void Complete(Bounds targetBounds)
    {
        if (!isPlaying)
            return;

        SetRevealEdge(targetBounds, 1f);
        isPlaying = false;

        StopEmitting(dust);
        StopEmitting(sparkle);
        StopEmitting(edgeDust);

        EmitFinishDustAndSparkle(targetBounds);

        if (finishBurst != null)
        {
            finishBurst.Clear(true);
            finishBurst.Emit(Mathf.Clamp(
                Mathf.RoundToInt(finishBurstAmount * Mathf.Max(.25f, activeSparkleAmount)),
                4,
                14
            ));
        }

        PlayFinishShine(targetBounds);

        cleanupRoutine = StartCoroutine(CleanupAfterFinish());
    }

    private void EmitFinishDustAndSparkle(Bounds targetBounds)
    {
        float width = Mathf.Max(.01f, targetBounds.size.x * activeWidthMultiplier);
        float lineHeight = Mathf.Max(.01f, targetBounds.size.y * .035f);
        float depth = Mathf.Max(.02f, targetBounds.size.z * .5f);
        int dustCount = Mathf.Clamp(
            Mathf.RoundToInt(6f * activeDustAmount),
            0,
            10
        );
        int sparkleCount = Mathf.Clamp(
            Mathf.RoundToInt(5f * activeSparkleAmount),
            0,
            10
        );

        SetShapeScale(dust, width * .65f, lineHeight * 2f, depth);
        SetShapeScale(sparkle, width * .65f, lineHeight * 2f, depth);

        if (dustCount > 0 && dust != null)
            dust.Emit(dustCount);

        if (sparkleCount > 0 && sparkle != null)
            sparkle.Emit(sparkleCount);
    }

    private void PlayFinishShine(Bounds targetBounds)
    {
        EnsureShineVisual(targetBounds);

        if (shineImage == null && shineRenderer == null)
            return;

        StopFinishShine();
        shineRoutine = StartCoroutine(FinishShineRoutine(targetBounds));
    }

    private IEnumerator FinishShineRoutine(Bounds targetBounds)
    {
        float duration = Mathf.Max(.05f, shineDuration);
        float angleRadians = shineAngle * Mathf.Deg2Rad;
        Vector2 lineDirection = new Vector2(
            Mathf.Cos(angleRadians),
            Mathf.Sin(angleRadians)
        );
        Vector2 sweepDirection = new Vector2(
            -lineDirection.y,
            lineDirection.x
        );
        float diagonal = new Vector2(
            targetBounds.size.x,
            targetBounds.size.y
        ).magnitude;
        float lineLength = Mathf.Max(.05f, diagonal * 1.45f);
        float sweepDistance = Mathf.Max(.05f, diagonal * 1.45f);
        float lineHeight = Mathf.Max(
            .02f,
            targetBounds.size.y * shineWidth
        );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            float offset = Mathf.Lerp(
                -sweepDistance * .5f,
                sweepDistance * .5f,
                easedProgress
            );
            Vector3 position = targetBounds.center + new Vector3(
                sweepDirection.x * offset,
                sweepDirection.y * offset,
                0f
            );
            Color color = shineColor;
            color.a *= Mathf.Sin(progress * Mathf.PI);

            SetShineVisual(
                position,
                angleRadians,
                lineLength,
                lineHeight,
                color
            );

            elapsed += useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            yield return null;
        }

        HideShineVisual();
        shineRoutine = null;
    }

    private void EnsureShineVisual(Bounds targetBounds)
    {
        if (shineImage != null || shineRenderer != null)
            return;

        Sprite sprite = GetShineSprite();
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            GameObject shineMaskObject = new GameObject(
                "FinishShineMask",
                typeof(RectTransform)
            );
            shineMaskObject.transform.SetParent(transform, false);

            RectTransform shineMaskRect =
                shineMaskObject.GetComponent<RectTransform>();
            Vector3 lossyScale = transform.lossyScale;
            shineMaskRect.anchorMin = new Vector2(.5f, .5f);
            shineMaskRect.anchorMax = new Vector2(.5f, .5f);
            shineMaskRect.pivot = new Vector2(.5f, .5f);
            shineMaskRect.position = targetBounds.center;
            shineMaskRect.sizeDelta = new Vector2(
                targetBounds.size.x /
                Mathf.Max(.0001f, Mathf.Abs(lossyScale.x)),
                targetBounds.size.y /
                Mathf.Max(.0001f, Mathf.Abs(lossyScale.y))
            );

            if (targetMaskSprite != null)
            {
                Image maskImage = shineMaskObject.AddComponent<Image>();
                maskImage.sprite = targetMaskSprite;
                maskImage.type = Image.Type.Simple;
                maskImage.color = Color.white;
                maskImage.raycastTarget = false;
                maskImage.maskable = true;

                Mask mask = shineMaskObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
            }
            else
            {
                shineMaskObject.AddComponent<RectMask2D>();
            }

            GameObject shineObject = new GameObject(
                "FinishShine",
                typeof(RectTransform)
            );
            shineObject.transform.SetParent(shineMaskObject.transform, false);

            shineImage = shineObject.AddComponent<Image>();
            shineImage.sprite = sprite;
            shineImage.material = GetUiShineMaterial();
            shineImage.raycastTarget = false;
            shineImage.maskable = true;
            shineImage.enabled = false;
            return;
        }

        GameObject worldShineObject = new GameObject("FinishShine");
        worldShineObject.transform.SetParent(transform, false);
        shineRenderer = worldShineObject.AddComponent<SpriteRenderer>();
        shineRenderer.sprite = sprite;
        shineRenderer.sharedMaterial = shineMaterial != null
            ? shineMaterial
            : GetFallbackParticleMaterial();
        shineRenderer.sortingLayerName = sortingLayerName;
        shineRenderer.sortingOrder = sortingOrder + 5;
        shineRenderer.enabled = false;
    }

    private void SetShineVisual(
        Vector3 position,
        float angleRadians,
        float lineLength,
        float lineHeight,
        Color color
    )
    {
        if (shineImage != null)
        {
            RectTransform rectTransform = shineImage.rectTransform;
            Vector3 lossyScale = transform.lossyScale;

            rectTransform.position = position;
            rectTransform.rotation = Quaternion.Euler(
                0f,
                0f,
                angleRadians * Mathf.Rad2Deg
            );
            rectTransform.sizeDelta = new Vector2(
                lineLength / Mathf.Max(.0001f, Mathf.Abs(lossyScale.x)),
                lineHeight / Mathf.Max(.0001f, Mathf.Abs(lossyScale.y))
            );
            shineImage.color = color;
            shineImage.enabled = color.a > .001f;
            return;
        }

        if (shineRenderer == null)
            return;

        Vector3 spriteSize = shineRenderer.sprite.bounds.size;
        Vector3 lossyScaleWorld = transform.lossyScale;
        shineRenderer.transform.position = position;
        shineRenderer.transform.rotation = Quaternion.Euler(
            0f,
            0f,
            angleRadians * Mathf.Rad2Deg
        );
        shineRenderer.transform.localScale = new Vector3(
            lineLength /
            Mathf.Max(.0001f, spriteSize.x * Mathf.Abs(lossyScaleWorld.x)),
            lineHeight /
            Mathf.Max(.0001f, spriteSize.y * Mathf.Abs(lossyScaleWorld.y)),
            1f
        );
        shineRenderer.color = color;
        shineRenderer.enabled = color.a > .001f;
    }

    private void StopFinishShine()
    {
        if (shineRoutine != null)
        {
            StopCoroutine(shineRoutine);
            shineRoutine = null;
        }

        HideShineVisual();
    }

    private void HideShineVisual()
    {
        if (shineImage != null)
            shineImage.enabled = false;

        if (shineRenderer != null)
            shineRenderer.enabled = false;
    }

    private Sprite GetShineSprite()
    {
        if (shineSprite != null)
            return shineSprite;

        const int textureWidth = 64;
        const int textureHeight = 16;
        shineTexture = new Texture2D(
            textureWidth,
            textureHeight,
            TextureFormat.RGBA32,
            false,
            true
        )
        {
            name = "BuildRevealShineTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[textureWidth * textureHeight];

        for (int y = 0; y < textureHeight; y++)
        {
            float normalizedY = ((y + .5f) / textureHeight) * 2f - 1f;
            float verticalAlpha = Mathf.Pow(
                Mathf.Clamp01(1f - Mathf.Abs(normalizedY)),
                1.35f
            );

            for (int x = 0; x < textureWidth; x++)
            {
                float normalizedX = ((x + .5f) / textureWidth) * 2f - 1f;
                float horizontalAlpha = Mathf.Pow(
                    Mathf.Clamp01(1f - Mathf.Abs(normalizedX)),
                    .65f
                );
                pixels[y * textureWidth + x] = new Color(
                    1f,
                    1f,
                    1f,
                    verticalAlpha * horizontalAlpha
                );
            }
        }

        shineTexture.SetPixels(pixels);
        shineTexture.Apply(false, true);
        shineSprite = Sprite.Create(
            shineTexture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(.5f, .5f),
            64f
        );
        shineSprite.name = "BuildRevealShineSprite";
        shineSprite.hideFlags = HideFlags.HideAndDontSave;
        return shineSprite;
    }

    private Material GetUiShineMaterial()
    {
        if (shineUiMaterial != null)
            return shineUiMaterial;

        Shader shader = Shader.Find("UI/Default");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        shineUiMaterial = new Material(shader)
        {
            name = "BuildRevealShine_UI",
            hideFlags = HideFlags.HideAndDontSave
        };
        return shineUiMaterial;
    }

    private static Sprite FindTargetSprite(Transform target)
    {
        if (target == null)
            return null;

        Image image = target.GetComponentInChildren<Image>(true);

        if (image != null && image.sprite != null)
            return image.sprite;

        SpriteRenderer spriteRenderer =
            target.GetComponentInChildren<SpriteRenderer>(true);
        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    public void StopAndDestroy()
    {
        if (cleanupRoutine != null)
        {
            StopCoroutine(cleanupRoutine);
            cleanupRoutine = null;
        }

        isPlaying = false;
        StopFinishShine();
        StopAndClear(dust);
        StopAndClear(sparkle);
        StopAndClear(edgeDust);
        StopAndClear(finishBurst);

        if (gameObject != null)
            Destroy(gameObject);
    }

    private IEnumerator CleanupAfterFinish()
    {
        float waitDuration = Mathf.Max(cleanupDelay, GetMaxParticleLifetime());

        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(waitDuration);
        else
            yield return new WaitForSeconds(waitDuration);

        StopAndClear(dust);
        StopAndClear(sparkle);
        StopAndClear(edgeDust);
        StopAndClear(finishBurst);
        StopFinishShine();
        cleanupRoutine = null;

        if (destroyAfterFinish && gameObject != null)
            Destroy(gameObject);
    }

    private void EnsureParticleSystems()
    {
        dust = FindOrCreateParticleSystem(dust, "Dust");
        sparkle = FindOrCreateParticleSystem(sparkle, "Sparkle");
        edgeDust = FindOrCreateParticleSystem(edgeDust, "EdgeDust");
        finishBurst = FindOrCreateParticleSystem(finishBurst, "FinishBurst");
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

        return particleSystem;
    }

    private void ConfigureParticleSystems()
    {
        ConfigureParticleSystem(
            dust,
            dustEmissionRate,
            .34f,
            .72f,
            .05f,
            .2f,
            .04f,
            .11f,
            new Color(1f, .67f, .18f, .8f),
            new Color(1f, .95f, .55f, 0f),
            dustMaterial,
            true,
            1201,
            -.05f,
            .18f
        );

        ConfigureParticleSystem(
            sparkle,
            sparkleEmissionRate,
            .28f,
            .58f,
            .08f,
            .18f,
            .035f,
            .075f,
            new Color(1f, .98f, .77f, .95f),
            new Color(1f, .78f, .3f, 0f),
            sparkleMaterial,
            true,
            1202,
            -.12f,
            .28f
        );

        ConfigureParticleSystem(
            edgeDust,
            edgeDustEmissionRate,
            .25f,
            .5f,
            .025f,
            .065f,
            .035f,
            .08f,
            new Color(1f, .76f, .25f, .7f),
            new Color(1f, .95f, .58f, 0f),
            edgeDustMaterial,
            true,
            1203,
            -.04f,
            .12f
        );

        ConfigureParticleSystem(
            finishBurst,
            0f,
            .35f,
            .68f,
            .12f,
            .34f,
            .05f,
            .11f,
            new Color(1f, .98f, .76f, 1f),
            new Color(1f, .67f, .2f, 0f),
            finishBurstMaterial,
            false,
            1204,
            -.18f,
            .4f
        );

        ConfigureParticleSystemRenderer(dust);
        ConfigureParticleSystemRenderer(sparkle);
        ConfigureParticleSystemRenderer(edgeDust);
        ConfigureParticleSystemRenderer(finishBurst);
    }

    private void ConfigureParticleSystem(
        ParticleSystem particleSystem,
        float emissionRate,
        float lifetimeMin,
        float lifetimeMax,
        float speedMin,
        float speedMax,
        float sizeMin,
        float sizeMax,
        Color startColorMin,
        Color startColorMax,
        Material material,
        bool looping,
        uint randomSeed,
        float velocityX,
        float velocityY
    )
    {
        if (particleSystem == null)
            return;

        particleSystem.randomSeed = randomSeed;
        particleSystem.useAutoRandomSeed = false;

        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 1f;
        main.loop = looping;
        main.prewarm = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = useUnscaledTime;
        main.maxParticles = 256;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            lifetimeMin,
            lifetimeMax
        );
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            speedMin,
            speedMax
        );
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(
            startColorMin,
            startColorMax
        );

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate);

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = Vector3.one;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            CreateLifetimeGradient(startColorMin, startColorMax)
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
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(
            -Mathf.Abs(velocityX),
            Mathf.Abs(velocityX)
        );
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(
            velocityY * .5f,
            velocityY
        );
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-.025f, .025f);

        particleSystem.Clear(true);
        SetShapeScale(particleSystem, 1f, 1f, .02f);
        ConfigureParticleSystemRenderer(particleSystem, material);
    }

    private void ConfigureParticleSystemRenderer(
        ParticleSystem particleSystem,
        Material material = null
    )
    {
        if (particleSystem == null)
            return;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();

        if (renderer == null)
            return;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.receiveShadows = false;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.allowRoll = true;

        Material resolvedMaterial = material;

        if (resolvedMaterial == null)
            resolvedMaterial = GetFallbackParticleMaterial();

        if (resolvedMaterial != null)
            renderer.sharedMaterial = resolvedMaterial;
    }

    private void ApplyEmissionRates()
    {
        SetEmissionRate(
            dust,
            dustEmissionRate * activeDustAmount * activeEmissionScale
        );
        SetEmissionRate(sparkle, sparkleEmissionRate * activeSparkleAmount);
        SetEmissionRate(
            edgeDust,
            edgeDustEmissionRate * activeDustAmount * activeEmissionScale
        );
    }

    private static void SetEmissionRate(ParticleSystem particleSystem, float rate)
    {
        if (particleSystem == null)
            return;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.Max(0f, rate));
    }

    private static void SetShapeScale(
        ParticleSystem particleSystem,
        float width,
        float height,
        float depth
    )
    {
        if (particleSystem == null)
            return;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.scale = new Vector3(width, height, depth);
    }

    private static void StopEmitting(ParticleSystem particleSystem)
    {
        if (particleSystem != null)
            particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    private static void StopAndClear(ParticleSystem particleSystem)
    {
        if (particleSystem != null)
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private float GetMaxParticleLifetime()
    {
        float maxLifetime = .5f;
        maxLifetime = Mathf.Max(maxLifetime, GetParticleLifetime(dust));
        maxLifetime = Mathf.Max(maxLifetime, GetParticleLifetime(sparkle));
        maxLifetime = Mathf.Max(maxLifetime, GetParticleLifetime(edgeDust));
        maxLifetime = Mathf.Max(maxLifetime, GetParticleLifetime(finishBurst));
        maxLifetime = Mathf.Max(maxLifetime, shineDuration);
        return maxLifetime;
    }

    private static float GetParticleLifetime(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
            return 0f;

        return particleSystem.main.startLifetime.constantMax;
    }

    private static Gradient CreateLifetimeGradient(
        Color startColor,
        Color endColor
    )
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(Color.Lerp(startColor, endColor, .6f), .55f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(startColor.a * .65f, .45f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return gradient;
    }

    private static AnimationCurve CreateSizeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, .45f),
            new Keyframe(.18f, 1f),
            new Keyframe(.72f, .75f),
            new Keyframe(1f, 0f)
        );
    }

    private static Material GetFallbackParticleMaterial()
    {
        Shader shader = Shader.Find("Particles/Standard Unlit");

        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        if (shader == null)
            return null;

        return new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
    }
}
