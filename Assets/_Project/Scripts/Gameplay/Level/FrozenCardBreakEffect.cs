using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;

public sealed class FrozenCardBreakEffect : MonoBehaviour
{
    private const int Columns = 3;
    private const int Rows = 3;
    private const float Duration = 0.72f;
    private const float Gravity = 2.4f;

    private sealed class Shard
    {
        public Transform Transform;
        public MeshRenderer Renderer;
        public Vector3 Velocity;
        public float AngularVelocity;
        public Vector3 BaseScale;
    }

    private readonly List<Shard> _shards = new List<Shard>();
    private readonly List<Mesh> _meshes = new List<Mesh>();
    private Color _sourceColor;

    public static void Play(SpriteRenderer source)
    {
        if (source == null || source.sprite == null)
            return;

        GameObject effectObject = new GameObject("FrozenCardBreakEffect");
        Transform effectTransform = effectObject.transform;
        effectTransform.SetParent(source.transform.parent, false);
        effectTransform.localPosition = source.transform.localPosition;
        effectTransform.localRotation = source.transform.localRotation;
        effectTransform.localScale = source.transform.localScale;

        FrozenCardBreakEffect effect =
            effectObject.AddComponent<FrozenCardBreakEffect>();
        effect.Build(source);
        effect.StartCoroutine(effect.Animate());
    }

    private void Build(SpriteRenderer source)
    {
        Sprite sprite = source.sprite;
        Bounds bounds = sprite.bounds;
        Vector4 outerUv = DataUtility.GetOuterUV(sprite);
        _sourceColor = source.color;

        Vector2[,] points = BuildFragmentPoints(bounds);
        int shardIndex = 0;

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                Vector2 bottomLeft = points[column, row];
                Vector2 bottomRight = points[column + 1, row];
                Vector2 topLeft = points[column, row + 1];
                Vector2 topRight = points[column + 1, row + 1];
                bool flipDiagonal = (row + column) % 2 == 0;

                if (flipDiagonal)
                {
                    CreateShard(source, sprite, outerUv,
                        bottomLeft, topLeft, topRight, shardIndex++);
                    CreateShard(source, sprite, outerUv,
                        bottomLeft, topRight, bottomRight, shardIndex++);
                }
                else
                {
                    CreateShard(source, sprite, outerUv,
                        bottomLeft, topLeft, bottomRight, shardIndex++);
                    CreateShard(source, sprite, outerUv,
                        topLeft, topRight, bottomRight, shardIndex++);
                }
            }
        }
    }

    private static Vector2[,] BuildFragmentPoints(Bounds bounds)
    {
        Vector2[,] points = new Vector2[Columns + 1, Rows + 1];

        for (int x = 0; x <= Columns; x++)
        {
            for (int y = 0; y <= Rows; y++)
            {
                float normalizedX = x / (float)Columns;
                float normalizedY = y / (float)Rows;
                float localX = Mathf.Lerp(bounds.min.x, bounds.max.x, normalizedX);
                float localY = Mathf.Lerp(bounds.min.y, bounds.max.y, normalizedY);

                if (x > 0 && x < Columns)
                    localX += Random.Range(-0.08f, 0.08f) * bounds.size.x;
                if (y > 0 && y < Rows)
                    localY += Random.Range(-0.08f, 0.08f) * bounds.size.y;

                points[x, y] = new Vector2(localX, localY);
            }
        }

        return points;
    }

    private void CreateShard(
        SpriteRenderer source,
        Sprite sprite,
        Vector4 outerUv,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        int shardIndex)
    {
        Vector2 center = (a + b + c) / 3f;
        Mesh mesh = new Mesh { name = $"FrozenShardMesh_{shardIndex}" };
        mesh.vertices = new[]
        {
            (Vector3)(a - center),
            (Vector3)(b - center),
            (Vector3)(c - center)
        };
        mesh.uv = new[]
        {
            ToUv(a, sprite.bounds, outerUv),
            ToUv(b, sprite.bounds, outerUv),
            ToUv(c, sprite.bounds, outerUv)
        };
        mesh.colors = new[] { Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.RecalculateBounds();
        _meshes.Add(mesh);

        GameObject shardObject = new GameObject($"IceShard_{shardIndex}");
        Transform shardTransform = shardObject.transform;
        shardTransform.SetParent(transform, false);
        shardTransform.localPosition = center;

        MeshFilter filter = shardObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = shardObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + 2 + shardIndex;

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", sprite.texture);
        properties.SetColor("_Color", source.color);
        renderer.SetPropertyBlock(properties);

        Vector2 outward = center.sqrMagnitude > 0.001f
            ? center.normalized
            : Random.insideUnitCircle.normalized;
        outward = (outward + Random.insideUnitCircle * 0.45f).normalized;

        _shards.Add(new Shard
        {
            Transform = shardTransform,
            Renderer = renderer,
            Velocity = new Vector3(
                outward.x * Random.Range(1.1f, 2.2f),
                outward.y * Random.Range(1.2f, 2.5f) + 0.45f,
                0f),
            AngularVelocity = Random.Range(-520f, 520f),
            BaseScale = Vector3.one * Random.Range(0.96f, 1.05f)
        });
    }

    private static Vector2 ToUv(Vector2 point, Bounds bounds, Vector4 outerUv)
    {
        float x = Mathf.InverseLerp(bounds.min.x, bounds.max.x, point.x);
        float y = Mathf.InverseLerp(bounds.min.y, bounds.max.y, point.y);
        return new Vector2(
            Mathf.Lerp(outerUv.x, outerUv.z, x),
            Mathf.Lerp(outerUv.y, outerUv.w, y));
    }

    private IEnumerator Animate()
    {
        float elapsed = 0f;

        while (elapsed < Duration)
        {
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            float progress = Mathf.Clamp01(elapsed / Duration);
            float fade = 1f - Mathf.SmoothStep(0.45f, 1f, progress);

            for (int i = 0; i < _shards.Count; i++)
            {
                Shard shard = _shards[i];
                shard.Velocity += Vector3.down * (Gravity * deltaTime);
                shard.Transform.localPosition += shard.Velocity * deltaTime;
                shard.Transform.Rotate(0f, 0f,
                    shard.AngularVelocity * deltaTime, Space.Self);
                shard.Transform.localScale = shard.BaseScale *
                    Mathf.Lerp(1f, 0.72f, progress);

                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                shard.Renderer.GetPropertyBlock(properties);
                Color color = _sourceColor;
                color.a *= fade;
                properties.SetColor("_Color", color);
                shard.Renderer.SetPropertyBlock(properties);
            }

            yield return null;
        }

        for (int i = 0; i < _meshes.Count; i++)
            Destroy(_meshes[i]);

        Destroy(gameObject);
    }
}
