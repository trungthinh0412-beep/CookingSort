using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Bounds quad for World/Custom/Screen Space Outline. Add to a child of a
/// SkeletonRenderer. An OutlineFeature must separately provide _OutlineSeeds.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[AddComponentMenu("CookingBlast/Skeleton Outline")]
public sealed class SkeletonOutline : MonoBehaviour
{
    [Tooltip("Parent skeleton. Automatically found when this field is empty.")]
    [SerializeField] private SkeletonRenderer skeleton;
    [Tooltip("Material using the original URP World/Custom/Screen Space Outline shader. Requires OutlineFeature.")]
    [SerializeField] private Material outlineMaterial;
    [Tooltip("Usually zero: the outline shader's Transparent-1 queue draws before the skeleton.")]
    [SerializeField] private int sortingOrderOffset;
    [Tooltip("Extra local bounds for culling only. The shader itself expands the visible quad in screen pixels.")]
    [SerializeField, Min(0f)] private float cullingPadding = 0.5f;

    private static readonly HashSet<SkeletonOutline> active = new HashSet<SkeletonOutline>();
    public static IEnumerable<SkeletonOutline> ActiveOutlines => active;
    public SkeletonRenderer SourceSkeleton => subscribedSkeleton;
    public MeshRenderer SourceRenderer => sourceRenderer;
    public Mesh SourceMesh => sourceFilter != null ? sourceFilter.sharedMesh : null;
    public Material OutlineMaterial => outlineMaterial;

    private SkeletonRenderer subscribedSkeleton;
    private MeshRenderer sourceRenderer;
    private MeshFilter sourceFilter;
    private MeshFilter quadFilter;
    private MeshRenderer quadRenderer;
    private Mesh quad;
    private Mesh previousMesh;
    private Material[] previousMaterials;
    private bool previousEnabled;
    private ShadowCastingMode previousShadowCasting;
    private bool previousReceiveShadows;
    private readonly Vector3[] vertices = new Vector3[4];

    private void Reset()
    {
        ResolveSource();
    }

    private void OnEnable()
    {
        if (GetComponent<SkeletonRenderer>() != null)
        {
            Debug.LogError("[SkeletonOutline] Add this component to a child GameObject, not the skeleton root.", this);
            return;
        }
        quadFilter = GetComponent<MeshFilter>();
        quadRenderer = GetComponent<MeshRenderer>();
        previousMesh = quadFilter.sharedMesh;
        previousMaterials = quadRenderer.sharedMaterials;
        previousEnabled = quadRenderer.enabled;
        previousShadowCasting = quadRenderer.shadowCastingMode;
        previousReceiveShadows = quadRenderer.receiveShadows;
        quad = new Mesh { name = "Skeleton screen outline quad", hideFlags = HideFlags.HideAndDontSave };
        quad.MarkDynamic();
        quad.vertices = vertices;
        // TEXCOORD0 is an outward corner direction, not atlas coordinates.
        quad.uv = new[] { new Vector2(-1,-1), new Vector2(1,-1), new Vector2(1,1), new Vector2(-1,1) };
        quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quadFilter.sharedMesh = quad;
        quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
        active.Add(this);
        RefreshOutline();
    }

    private void ResolveSource()
    {
        // Must be a child: using the source's own MeshFilter would replace
        // Spine's animated mesh with the bounds quad.
        if (skeleton == null && transform.parent != null)
            skeleton = transform.parent.GetComponentInParent<SkeletonRenderer>();
        SkeletonRenderer next = skeleton != null && skeleton.gameObject != gameObject ? skeleton : null;
        if (next == subscribedSkeleton) return;
        Unsubscribe();
        subscribedSkeleton = next;
        sourceRenderer = next != null ? next.GetComponent<MeshRenderer>() : null;
        sourceFilter = next != null ? next.GetComponent<MeshFilter>() : null;
        if (next == null) return;
        next.OnMeshAndMaterialsUpdated += OnMeshUpdated;
        next.OnRebuild += OnMeshUpdated;
    }

    private void OnMeshUpdated(SkeletonRenderer source) { RefreshOutline(); }
    private void LateUpdate() { RefreshOutline(); }

    public void RefreshOutline()
    {
        if (!isActiveAndEnabled || quad == null || quadRenderer == null) return;
        ResolveSource();
        Mesh mesh = SourceMesh;
        bool visible = subscribedSkeleton != null && subscribedSkeleton.isActiveAndEnabled &&
            subscribedSkeleton.valid && subscribedSkeleton.Skeleton != null &&
            subscribedSkeleton.Skeleton.A > 0.0001f && sourceRenderer != null &&
            sourceRenderer.enabled && !sourceRenderer.forceRenderingOff &&
            mesh != null && mesh.vertexCount > 0 && outlineMaterial != null;
        quadRenderer.enabled = visible;
        if (!visible) return;

        Bounds bounds = mesh.bounds;
        Vector3 min = bounds.min, max = bounds.max;
        float z = bounds.center.z;
        vertices[0] = ToQuadSpace(new Vector3(min.x, min.y, z));
        vertices[1] = ToQuadSpace(new Vector3(max.x, min.y, z));
        vertices[2] = ToQuadSpace(new Vector3(max.x, max.y, z));
        vertices[3] = ToQuadSpace(new Vector3(min.x, max.y, z));
        quad.vertices = vertices;
        quad.RecalculateBounds();
        Bounds paddedBounds = quad.bounds;
        paddedBounds.Expand(cullingPadding * 2f);
        quad.bounds = paddedBounds;
        if (quadRenderer.sharedMaterial != outlineMaterial)
            quadRenderer.sharedMaterial = outlineMaterial;
        quadRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        quadRenderer.sortingOrder = Mathf.Clamp(sourceRenderer.sortingOrder + sortingOrderOffset, -32768, 32767);
    }

    private Vector3 ToQuadSpace(Vector3 sourceLocal)
    {
        return transform.InverseTransformPoint(subscribedSkeleton.transform.TransformPoint(sourceLocal));
    }

    private void Unsubscribe()
    {
        if (subscribedSkeleton != null)
        {
            subscribedSkeleton.OnMeshAndMaterialsUpdated -= OnMeshUpdated;
            subscribedSkeleton.OnRebuild -= OnMeshUpdated;
        }
        subscribedSkeleton = null;
        sourceRenderer = null;
        sourceFilter = null;
    }

    private void OnDisable()
    {
        active.Remove(this);
        Unsubscribe();
        if (quadFilter != null && quadFilter.sharedMesh == quad)
            quadFilter.sharedMesh = previousMesh;
        if (quadRenderer != null)
        {
            quadRenderer.sharedMaterials = previousMaterials ?? new Material[0];
            quadRenderer.enabled = previousEnabled;
            quadRenderer.shadowCastingMode = previousShadowCasting;
            quadRenderer.receiveShadows = previousReceiveShadows;
        }
        if (quad != null)
        {
            if (Application.isPlaying) Destroy(quad); else DestroyImmediate(quad);
            quad = null;
        }
    }
}
