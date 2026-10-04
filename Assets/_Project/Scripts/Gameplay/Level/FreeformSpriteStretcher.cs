using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FreeformSpriteStretcher : MonoBehaviour
{
    [Tooltip("Change this Sprite at any time via Inspector or code.")]
    public Sprite sourceSprite;
    
    // We track the previous sprite to detect when it changes
    private Sprite previousSprite;

    [Header("Corner Offsets (Local Space)")]
    public Vector3 bottomLeftOffset;
    public Vector3 bottomRightOffset;
    public Vector3 topLeftOffset;
    public Vector3 topRightOffset;

    private Mesh dynamicMesh;
    private MeshRenderer meshRenderer;
    private Vector3[] baseVertices;

    void Start()
    {
        // 1. Initialize empty mesh and renderer
        dynamicMesh = new Mesh();
        dynamicMesh.name = "Stretched Sprite Quad";
        GetComponent<MeshFilter>().mesh = dynamicMesh;

        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Sprites/Default"));

        // 2. Run the setup for the first time
        RefreshSpriteData();
    }

    void Update()
    {
        // 3. Detect if the sprite was changed via code or the Inspector this frame
        if (sourceSprite != previousSprite)
        {
            RefreshSpriteData();
        }

        UpdateMesh();
    }

    /// <summary>
    /// Recalculates the base bounds, material texture, and UVs for the current Sprite.
    /// You can also call this manually if you want to force a refresh.
    /// </summary>
    public void RefreshSpriteData()
    {
        previousSprite = sourceSprite;

        // Handle the case where the sprite is removed/set to null
        if (sourceSprite == null)
        {
            meshRenderer.enabled = false;
            return;
        }

        meshRenderer.enabled = true;
        meshRenderer.material.mainTexture = sourceSprite.texture;

        // Recalculate base positions based on the NEW Sprite's size
        Bounds bounds = sourceSprite.bounds;
        float extX = bounds.extents.x;
        float extY = bounds.extents.y;

        baseVertices = new Vector3[4]
        {
            new Vector3(-extX, -extY, 0), // Bottom-Left
            new Vector3(extX, -extY, 0),  // Bottom-Right
            new Vector3(-extX, extY, 0),  // Top-Left
            new Vector3(extX, extY, 0)    // Top-Right
        };

        // Map the UVs for the NEW Sprite (crucial if using Sprite Atlases)
        Rect rect = sourceSprite.textureRect;
        float texW = sourceSprite.texture.width;
        float texH = sourceSprite.texture.height;

        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(rect.xMin / texW, rect.yMin / texH),
            new Vector2(rect.xMax / texW, rect.yMin / texH),
            new Vector2(rect.xMin / texW, rect.yMax / texH),
            new Vector2(rect.xMax / texW, rect.yMax / texH)
        };

        // Apply structural data to the mesh
        dynamicMesh.vertices = baseVertices;
        dynamicMesh.uv = uvs;
        dynamicMesh.triangles = new int[6] { 0, 2, 1, 2, 3, 1 };
    }

    private void UpdateMesh()
    {
        // Don't try to stretch if we have no active sprite data
        if (dynamicMesh == null || baseVertices == null || sourceSprite == null) return;

        // Apply the inspector offsets to our clean baseline vertices
        Vector3[] currentVertices = new Vector3[4];
        currentVertices[0] = baseVertices[0] + bottomLeftOffset;
        currentVertices[1] = baseVertices[1] + bottomRightOffset;
        currentVertices[2] = baseVertices[2] + topLeftOffset;
        currentVertices[3] = baseVertices[3] + topRightOffset;

        // Push to the GPU
        dynamicMesh.vertices = currentVertices;
        dynamicMesh.RecalculateBounds(); 
    }
}