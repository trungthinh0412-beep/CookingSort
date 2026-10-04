using UnityEngine;

[DisallowMultipleComponent]
public sealed class CardBendSurface : MonoBehaviour
{
    private const int Columns = 16;
    private const string SurfaceName = "FlipBendSurface";

    private MeshFilter _filter;
    private MeshRenderer _renderer;
    private Mesh _mesh;
    private MaterialPropertyBlock _block;

    private Vector3[] _vertices;
    private Vector2[] _uv;
    private Color[] _colors;

    private Sprite _sprite;
    private Vector2 _min;
    private Vector2 _max;

    private Vector2 _uvOrigin;
    private Vector2 _uvPerX;
    private Vector2 _uvPerY;
    private bool _hasUvMap;

    public static CardBendSurface For(Card card)
    {
        if (card == null) return null;

        CardBendSurface surface = card.GetComponentInChildren<CardBendSurface>(true);
        if (surface != null) return surface;

        GameObject host = new GameObject(SurfaceName);
        host.layer = card.gameObject.layer;
        host.transform.SetParent(card.transform, false);
        host.transform.localPosition = Vector3.zero;
        host.transform.localRotation = Quaternion.identity;
        host.transform.localScale = Vector3.one;

        return host.AddComponent<CardBendSurface>();
    }

    private void Awake()
    {
        _filter = gameObject.AddComponent<MeshFilter>();
        _renderer = gameObject.AddComponent<MeshRenderer>();
        _renderer.enabled = false;

        _mesh = new Mesh { name = SurfaceName };
        _mesh.MarkDynamic();
        _filter.sharedMesh = _mesh;
        _block = new MaterialPropertyBlock();

        BuildTopology();
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }

    public void Begin(SpriteRenderer source, Sprite sprite)
    {
        if (source == null || sprite == null)
            return;

        _renderer.sharedMaterial = source.sharedMaterial;
        _renderer.sortingLayerID = source.sortingLayerID;
        _renderer.sortingOrder = source.sortingOrder;
        _renderer.enabled = true;

        SetSprite(sprite);
    }

    public void SetSprite(Sprite sprite)
    {
        if (sprite == null || sprite == _sprite)
            return;

        _sprite = sprite;

        Bounds bounds = sprite.bounds;
        _min = bounds.min;
        _max = bounds.max;

        SolveUvMap(sprite);

        _block.SetTexture("_MainTex", sprite.texture);
        _block.SetColor("_RendererColor", Color.white);
        _block.SetColor("_Color", Color.white);
        _block.SetVector("_Flip", new Vector4(1f, 1f, 1f, 1f));
        _renderer.SetPropertyBlock(_block);
    }

    public void SetPose(float yawDegrees, float curveDegrees, float shadeFloor)
    {
        if (_sprite == null || !_renderer.enabled)
            return;

        float width = _max.x - _min.x;
        if (width <= 0.0001f)
            return;

        float yaw = yawDegrees * Mathf.Deg2Rad;
        float curvature = (curveDegrees * Mathf.Deg2Rad) / width;
        bool mirrored = Mathf.Cos(yaw) < 0f;

        float sinYaw = Mathf.Sin(yaw);
        float cosYaw = Mathf.Cos(yaw);
        bool curved = Mathf.Abs(curvature) > 0.0001f;

        float drift = curved
            ? sinYaw * (Mathf.Cos(curveDegrees * Mathf.Deg2Rad * 0.5f) - 1f) / curvature
            : 0f;

        float centreX = (_min.x + _max.x) * 0.5f;

        for (int column = 0; column <= Columns; column++)
        {
            float u = column / (float)Columns;
            float arc = (u - 0.5f) * width;
            float angle = yaw + curvature * arc;

            float offset = curved
                ? (Mathf.Sin(angle) - sinYaw) / curvature
                : arc * cosYaw;

            float x = centreX + offset - drift;

            float shade = Mathf.Lerp(
                shadeFloor,
                1f,
                Mathf.Abs(Mathf.Cos(angle))
            );
            Color tint = new Color(shade, shade, shade, 1f);

            float sampleX = Mathf.Lerp(_min.x, _max.x, mirrored ? 1f - u : u);

            int bottom = column * 2;
            int top = bottom + 1;

            _vertices[bottom] = new Vector3(x, _min.y, 0f);
            _vertices[top] = new Vector3(x, _max.y, 0f);
            _uv[bottom] = ToUv(sampleX, _min.y);
            _uv[top] = ToUv(sampleX, _max.y);
            _colors[bottom] = tint;
            _colors[top] = tint;
        }

        _mesh.vertices = _vertices;
        _mesh.uv = _uv;
        _mesh.colors = _colors;
        _mesh.RecalculateBounds();
    }

    public void End()
    {
        if (_renderer != null)
            _renderer.enabled = false;
    }

    private void BuildTopology()
    {
        int vertexCount = (Columns + 1) * 2;
        _vertices = new Vector3[vertexCount];
        _uv = new Vector2[vertexCount];
        _colors = new Color[vertexCount];

        int[] triangles = new int[Columns * 6];
        for (int column = 0; column < Columns; column++)
        {
            int v = column * 2;
            int t = column * 6;

            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 3;
            triangles[t + 3] = v;
            triangles[t + 4] = v + 3;
            triangles[t + 5] = v + 2;
        }

        _mesh.vertices = _vertices;
        _mesh.uv = _uv;
        _mesh.colors = _colors;
        _mesh.triangles = triangles;
    }

    private Vector2 ToUv(float x, float y)
    {
        if (!_hasUvMap)
            return Vector2.zero;

        return _uvOrigin + _uvPerX * x + _uvPerY * y;
    }

    private void SolveUvMap(Sprite sprite)
    {
        _hasUvMap = false;

        Vector2[] points = sprite.vertices;
        Vector2[] uvs = sprite.uv;
        if (points == null || uvs == null ||
            points.Length < 3 || uvs.Length < points.Length)
        {
            return;
        }

        Vector2 p0 = points[0];
        for (int i = 1; i < points.Length - 1; i++)
        {
            Vector2 a = points[i] - p0;
            Vector2 b = points[i + 1] - p0;
            float determinant = a.x * b.y - a.y * b.x;

            if (Mathf.Abs(determinant) < 1e-9f)
                continue;

            Vector2 ua = uvs[i] - uvs[0];
            Vector2 ub = uvs[i + 1] - uvs[0];

            _uvPerX = (ua * b.y - ub * a.y) / determinant;
            _uvPerY = (ub * a.x - ua * b.x) / determinant;
            _uvOrigin = uvs[0] - _uvPerX * p0.x - _uvPerY * p0.y;
            _hasUvMap = true;
            return;
        }
    }
}