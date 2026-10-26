using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

namespace CookingSort2D
{
    /// <summary>
    /// Outlines the union of the current Spine attachments, rather than sampling
    /// neighbouring atlas regions. The original SkeletonRenderer stays in charge
    /// of animation and colour rendering. Intended for XY-plane world skeletons.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkeletonRenderer), typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("CookingBlast/Spine Silhouette Outline")]
    public sealed class SpineSilhouetteOutline : MonoBehaviour
    {
        [Header("White outline")]
        [SerializeField] private Color outlineColor = Color.white;
        [Tooltip("Width in reference pixels. It follows the character's Transform scale.")]
        [SerializeField, Range(0f, 24f)] private float outlineWidth = 6f;
        [SerializeField, Range(0.25f, 8f)] private float softness = 1.25f;

        [Header("Optional outer glow")]
        [SerializeField] private Color glowColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField, Range(0f, 24f)] private float glowWidth = 4f;
        [SerializeField, Range(0f, 1f)] private float glowStrength = 0.15f;

        [Header("Quality and size")]
        [Tooltip("256 is cheaper; 512 is sharper for close-up characters. Textures are reused.")]
        [SerializeField, Range(128, 1024)] private int maskResolution = 512;
        [Tooltip("Reference pixels per local Unity unit. Keeps thickness stable during animation.")]
        [SerializeField, Min(1f)] private float pixelsPerUnit = 128f;
        [Tooltip("Ignore almost-transparent attachment pixels when forming the silhouette.")]
        [SerializeField, Range(0.01f, 0.95f)] private float alphaThreshold = 0.1f;
        [Tooltip("Draw immediately behind the source Skeleton on the same Sorting Layer.")]
        [SerializeField] private int sortingOrderOffset = -1;

        [Header("Shaders (assigned automatically on Reset)")]
        [SerializeField] private Shader maskShader;
        [SerializeField] private Shader distanceShader;
        [SerializeField] private Shader outlineShader;

        private SkeletonRenderer skeletonRenderer;
        private MeshRenderer sourceRenderer;
        private MeshFilter sourceFilter;
        private GameObject outlineObject;
        private MeshRenderer outlineRenderer;
        private Mesh outlineMesh;
        private Material maskMaterial;
        private Material distanceMaterial;
        private Material outlineMaterial;
        private RenderTexture maskTexture;
        private RenderTexture distanceA;
        private RenderTexture distanceB;
        private CommandBuffer commands;
        private MaterialPropertyBlock drawProperties;
        private readonly List<Material> sourceMaterials = new List<Material>();
        private readonly Vector3[] quadVertices = new Vector3[4];
        private bool dirty = true;
        private bool rendering;
        private bool reportedUnsupported;

        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int LocalBounds = Shader.PropertyToID("_LocalBounds");
        private static readonly int SkeletonAlpha = Shader.PropertyToID("_SkeletonAlpha");

        public int MaskResolution => maskTexture != null ? maskTexture.width : maskResolution;

        private void Reset()
        {
            FindShaders();
            dirty = true;
        }

        private void FindShaders()
        {
            if (maskShader == null) maskShader = Shader.Find("Hidden/CookingBlast/Spine Silhouette Mask");
            if (distanceShader == null) distanceShader = Shader.Find("Hidden/CookingBlast/Spine Silhouette Distance");
            if (outlineShader == null) outlineShader = Shader.Find("CookingBlast/Spine/Silhouette Outline");
        }

        private void OnEnable()
        {
            skeletonRenderer = GetComponent<SkeletonRenderer>();
            sourceRenderer = GetComponent<MeshRenderer>();
            sourceFilter = GetComponent<MeshFilter>();
            skeletonRenderer.OnMeshAndMaterialsUpdated += OnMeshUpdated;
            skeletonRenderer.OnRebuild += OnSkeletonRebuilt;
            dirty = true;
        }

        private void OnValidate()
        {
            maskResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(maskResolution), 128, 1024);
            pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            dirty = true;
        }

        private void OnSkeletonRebuilt(SkeletonRenderer source) { dirty = true; }

        private void OnMeshUpdated(SkeletonRenderer source)
        {
            dirty = true;
            // Spine raises this after updating both its mesh AND materials.
            // Drawing here avoids the one-frame lag caused by independent LateUpdates.
            RefreshOutline();
        }

        private void LateUpdate()
        {
            if (outlineRenderer != null)
            {
                outlineObject.layer = gameObject.layer;
                outlineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                outlineRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
                outlineRenderer.enabled = sourceRenderer.enabled && !sourceRenderer.forceRenderingOff &&
                    skeletonRenderer.enabled && skeletonRenderer.valid;
            }
            if (dirty) RefreshOutline();
        }

        /// <summary>Also usable by editor previews after manually evaluating a pose.</summary>
        public void RefreshOutline()
        {
            if (rendering || !isActiveAndEnabled || sourceRenderer == null ||
                sourceFilter == null || skeletonRenderer == null) return;

            Mesh mesh = sourceFilter.sharedMesh; // Spine alternates between two meshes.
            float opacity = skeletonRenderer.Skeleton != null ? skeletonRenderer.Skeleton.A : 0f;
            bool visible = sourceRenderer.enabled && !sourceRenderer.forceRenderingOff &&
                skeletonRenderer.enabled && skeletonRenderer.valid && mesh != null &&
                mesh.vertexCount > 0 && opacity > 0.0001f &&
                (outlineWidth > 0f || (glowWidth > 0f && glowStrength > 0f));
            if (!visible)
            {
                if (outlineRenderer != null) outlineRenderer.enabled = false;
                return;
            }
            if (!EnsureResources()) return;

            rendering = true;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                Bounds bounds = mesh.bounds;
                float glow = glowStrength > 0f ? glowWidth : 0f;
                float padding = (outlineWidth + glow + softness + 3f) / pixelsPerUnit;
                float side = Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.y)) + padding * 2f;
                Vector2 center = new Vector2(bounds.center.x, bounds.center.y);
                float pixelsPerLocalUnit = maskTexture.width / side;
                float pixelScale = pixelsPerLocalUnit / pixelsPerUnit;

                commands.Clear();
                commands.SetRenderTarget(maskTexture);
                commands.SetViewport(new Rect(0, 0, maskTexture.width, maskTexture.height));
                commands.ClearRenderTarget(false, true, Color.clear);
                sourceRenderer.GetSharedMaterials(sourceMaterials);
                for (int submesh = 0; submesh < mesh.subMeshCount && submesh < sourceMaterials.Count; submesh++)
                {
                    Material source = sourceMaterials[submesh];
                    if (source == null || source.mainTexture == null) continue;
                    drawProperties.Clear();
                    drawProperties.SetTexture(MainTex, source.mainTexture);
                    drawProperties.SetVector(LocalBounds, new Vector4(center.x, center.y, side, side));
                    drawProperties.SetFloat(SkeletonAlpha, opacity);
                    commands.DrawMesh(mesh, Matrix4x4.identity, maskMaterial, submesh, 0, drawProperties);
                }
                Graphics.ExecuteCommandBuffer(commands);

                distanceMaterial.SetFloat("_AlphaThreshold", alphaThreshold);
                Graphics.Blit(maskTexture, distanceA, distanceMaterial, 0);
                RenderTexture read = distanceA;
                RenderTexture write = distanceB;
                int reach = Mathf.Clamp(Mathf.CeilToInt((outlineWidth + glow + softness + 2f) * pixelScale), 1, maskTexture.width);
                for (int step = Mathf.NextPowerOfTwo(reach); step >= 1; step >>= 1)
                    JumpFlood(ref read, ref write, step);
                // Extra unit step reduces the few corner errors of ordinary jump flooding.
                JumpFlood(ref read, ref write, 1);

                outlineMaterial.SetTexture(MainTex, read);
                outlineMaterial.SetTexture("_MaskTex", maskTexture);
                outlineMaterial.SetColor("_OutlineColor", outlineColor);
                outlineMaterial.SetFloat("_OutlineWidth", outlineWidth * pixelScale);
                outlineMaterial.SetFloat("_Softness", softness * pixelScale);
                outlineMaterial.SetColor("_GlowColor", glowColor);
                outlineMaterial.SetFloat("_GlowWidth", glow * pixelScale);
                outlineMaterial.SetFloat("_GlowStrength", glowStrength);
                outlineMaterial.SetFloat("_Opacity", opacity);

                float half = side * 0.5f;
                float z = bounds.center.z;
                quadVertices[0] = new Vector3(center.x - half, center.y - half, z);
                quadVertices[1] = new Vector3(center.x + half, center.y - half, z);
                quadVertices[2] = new Vector3(center.x + half, center.y + half, z);
                quadVertices[3] = new Vector3(center.x - half, center.y + half, z);
                outlineMesh.vertices = quadVertices;
                outlineMesh.RecalculateBounds();
                outlineObject.layer = gameObject.layer;
                outlineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                outlineRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
                outlineRenderer.enabled = true;
                dirty = false;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                rendering = false;
            }
        }

        private void JumpFlood(ref RenderTexture read, ref RenderTexture write, int step)
        {
            distanceMaterial.SetFloat("_Step", step);
            Graphics.Blit(read, write, distanceMaterial, 1);
            RenderTexture swap = read;
            read = write;
            write = swap;
        }

        private bool EnsureResources()
        {
            FindShaders();
            RenderTextureFormat fieldFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGBFloat;
            if (maskShader == null || distanceShader == null || outlineShader == null ||
                !maskShader.isSupported || !distanceShader.isSupported || !outlineShader.isSupported ||
                !SystemInfo.SupportsRenderTextureFormat(fieldFormat))
            {
                if (!reportedUnsupported)
                {
                    Debug.LogWarning("[SpineSilhouetteOutline] Outline shaders/float render textures unavailable. Original Skeleton remains visible.", this);
                    reportedUnsupported = true;
                }
                if (outlineRenderer != null) outlineRenderer.enabled = false;
                return false;
            }
            reportedUnsupported = false;
            if (maskMaterial == null) maskMaterial = NewMaterial(maskShader);
            if (distanceMaterial == null) distanceMaterial = NewMaterial(distanceShader);
            if (outlineMaterial == null) outlineMaterial = NewMaterial(outlineShader);
            if (commands == null) commands = new CommandBuffer { name = "Spine silhouette mask" };
            if (drawProperties == null) drawProperties = new MaterialPropertyBlock();

            int resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(maskResolution), 128, 1024);
            if (maskTexture == null || maskTexture.width != resolution || !maskTexture.IsCreated())
            {
                ReleaseTextures();
                maskTexture = NewTexture(resolution, RenderTextureFormat.ARGB32, FilterMode.Bilinear, "Spine alpha mask");
                distanceA = NewTexture(resolution, fieldFormat, FilterMode.Point, "Spine distance A");
                distanceB = NewTexture(resolution, fieldFormat, FilterMode.Point, "Spine distance B");
            }
            if (outlineObject == null)
            {
                outlineObject = new GameObject("Silhouette Outline (generated)");
                outlineObject.hideFlags = HideFlags.HideAndDontSave;
                outlineObject.transform.SetParent(transform, false);
                outlineMesh = new Mesh { name = "Spine silhouette quad", hideFlags = HideFlags.HideAndDontSave };
                outlineMesh.MarkDynamic();
                outlineMesh.vertices = quadVertices;
                outlineMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                outlineMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                outlineObject.AddComponent<MeshFilter>().sharedMesh = outlineMesh;
                outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
                outlineRenderer.sharedMaterial = outlineMaterial;
                outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                outlineRenderer.receiveShadows = false;
                outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
                outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            return true;
        }

        private static Material NewMaterial(Shader shader)
        {
            return new Material(shader) { name = shader.name + " (instance)", hideFlags = HideFlags.HideAndDontSave };
        }

        private static RenderTexture NewTexture(int resolution, RenderTextureFormat format, FilterMode filtering, string label)
        {
            var texture = new RenderTexture(resolution, resolution, 0, format, RenderTextureReadWrite.Linear)
            {
                name = label,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = filtering,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };
            texture.Create();
            return texture;
        }

        private void OnDisable()
        {
            if (skeletonRenderer != null)
            {
                skeletonRenderer.OnMeshAndMaterialsUpdated -= OnMeshUpdated;
                skeletonRenderer.OnRebuild -= OnSkeletonRebuilt;
            }
            ReleaseResources();
        }

        private void OnDestroy() { ReleaseResources(); }

        private void ReleaseTextures()
        {
            ReleaseTexture(ref maskTexture);
            ReleaseTexture(ref distanceA);
            ReleaseTexture(ref distanceB);
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            DestroyOwned(texture);
            texture = null;
        }

        private void ReleaseResources()
        {
            if (outlineRenderer != null) outlineRenderer.enabled = false;
            DestroyOwned(outlineObject);
            DestroyOwned(outlineMesh);
            DestroyOwned(maskMaterial);
            DestroyOwned(distanceMaterial);
            DestroyOwned(outlineMaterial);
            outlineObject = null;
            outlineRenderer = null;
            outlineMesh = null;
            maskMaterial = null;
            distanceMaterial = null;
            outlineMaterial = null;
            ReleaseTextures();
            commands?.Release();
            commands = null;
            dirty = true;
        }

        private static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
