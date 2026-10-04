using System.Collections;
using UnityEngine;

public sealed class DarkKingExplosionEffect : MonoBehaviour
{
    private Texture2D _generatedTexture;
    private Sprite _generatedSprite;

    public static void CreateScorchMark(
        Transform parent,
        Vector3 localPosition,
        Vector3 holderScale)
    {
        if (parent == null)
            return;

        GameObject markObject = new GameObject("DarkKingScorchMark");
        Transform markTransform = markObject.transform;
        markTransform.SetParent(parent, false);
        markTransform.localPosition = localPosition;
        markTransform.localRotation = Quaternion.identity;
        markTransform.localScale = new Vector3(
            Mathf.Abs(holderScale.x) * 1.18f,
            Mathf.Abs(holderScale.y) * 0.88f,
            1f
        );

        DarkKingExplosionEffect cleanup =
            markObject.AddComponent<DarkKingExplosionEffect>();
        SpriteRenderer renderer = markObject.AddComponent<SpriteRenderer>();
        cleanup.Build(renderer);
        cleanup.StartCoroutine(cleanup.FadeAndDestroy(renderer));
    }

    private void Build(SpriteRenderer renderer)
    {
        const int size = 128;
        _generatedTexture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false
        )
        {
            name = "DarkKingScorchTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        int seed = Mathf.Abs(GetInstanceID());

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float angle = Mathf.Atan2(ny, nx);
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float edge = 0.72f +
                    Mathf.Sin(angle * 5f + seed * 0.013f) * 0.08f +
                    Mathf.Sin(angle * 9f - seed * 0.007f) * 0.045f;
                float noise = Hash01(x, y, seed);
                float alpha = Mathf.Clamp01((edge - radius) * 7f);
                alpha *= Mathf.Lerp(0.7f, 1f, noise);

                float hotEdge = Mathf.Clamp01((radius - 0.38f) * 2.2f);
                byte red = (byte)Mathf.Lerp(19f, 48f, hotEdge);
                byte green = (byte)Mathf.Lerp(14f, 27f, hotEdge);
                byte blue = (byte)Mathf.Lerp(16f, 18f, hotEdge);
                pixels[y * size + x] = new Color32(
                    red,
                    green,
                    blue,
                    (byte)(alpha * 220f)
                );
            }
        }

        AddSootSpeckles(pixels, size, seed);
        _generatedTexture.SetPixels32(pixels);
        _generatedTexture.Apply(false, true);

        _generatedSprite = Sprite.Create(
            _generatedTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
        _generatedSprite.name = "DarkKingScorchSprite";

        renderer.sprite = _generatedSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 5;
    }

    private IEnumerator FadeAndDestroy(SpriteRenderer renderer)
    {
        const float fadeDuration = 5f;
        float elapsed = 0f;
        Color startColor = renderer.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeDuration);
            Color color = startColor;
            color.a = 1f - Mathf.SmoothStep(0f, 1f, progress);
            renderer.color = color;
            yield return null;
        }

        Destroy(gameObject);
    }

    private static void AddSootSpeckles(Color32[] pixels, int size, int seed)
    {
        System.Random random = new System.Random(seed);

        for (int i = 0; i < 34; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(0.62f, 0.98f,
                (float)random.NextDouble());
            int centerX = Mathf.RoundToInt(
                (0.5f + Mathf.Cos(angle) * distance * 0.5f) * size
            );
            int centerY = Mathf.RoundToInt(
                (0.5f + Mathf.Sin(angle) * distance * 0.5f) * size
            );
            int radius = random.Next(1, 4);

            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int px = centerX + x;
                    int py = centerY + y;
                    if (px < 0 || px >= size || py < 0 || py >= size ||
                        x * x + y * y > radius * radius)
                    {
                        continue;
                    }

                    pixels[py * size + px] = new Color32(20, 14, 15, 185);
                }
            }
        }
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint value = (uint)(x * 374761393 + y * 668265263 + seed * 69069);
            value = (value ^ (value >> 13)) * 1274126177u;
            return (value & 0x00FFFFFF) / 16777215f;
        }
    }

    private void OnDestroy()
    {
        if (_generatedSprite != null)
            Destroy(_generatedSprite);
        if (_generatedTexture != null)
            Destroy(_generatedTexture);
    }
}
